---
title: 'Complete archive process integration evidence'
type: 'chore'
created: '2026-08-28'
status: 'done'
baseline_revision: '1618b846cbcddaf7ad69dfefbcbe34cd63df2ee1'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/adrs/0001-folder-domain-processor-persistence.md'
warnings:
  - multiple-goals
deferred:
  - summary: >-
      ArchiveFolderProcessWiringTests is not selected by a blocking CI lane.
    evidence: |-
      The integration class was already excluded before this bundle: the blocking parity script selects GoldenLifecycleParityTests, CrossAdapterBehavioralParityTests, and MixedSurfaceHandoffTests, while the baseline allow-list omits the IntegrationTests project. The four new archive-process rows therefore run locally but inherit the pre-existing CI selection gap.
    location: >-
      tests/tools/run-contract-parity-ci-gates.ps1
    severity: medium
  - summary: >-
      The shared gateway double's new `/process` result-payload propagation is asserted by no
      gate-selected test.
    evidence: |-
      `InProcessRejectionPropagatingGatewayClient` now forwards `ResultPayload`, which is the sole
      input to the REST `idempotentReplay` field. The only assertion on it lives in
      `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted`, in the
      class no blocking lane selects. `MixedSurfaceHandoffTests` is gate-selected and drives the same
      double through a four-surface replay, but asserts 202 / no conflict / no second append without
      ever reading `idempotentReplay`. Reverting the propagation would leave every gate green while
      every replay silently reported `idempotentReplay: false`. Closing this means adding an assertion
      to `MixedSurfaceHandoffTests`, which is outside this intent's "add four archive tests" approach.
    location: >-
      tests/Hexalith.Folders.IntegrationTests/MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs
    severity: medium
  - summary: >-
      Only the `Denied` archive-policy outcome has an integration row; the retryable outcomes have
      none at the `/process` boundary.
    evidence: |-
      `FolderArchivePolicyOutcome` also carries `ScopeMismatch`, `Unavailable`, `Malformed`, and
      `Stale`, and `EvaluatePolicy` additionally returns `PolicyEvidenceMalformed` when an *Allowed*
      evidence's tenant/organization/folder do not match the command or its `PolicyVersion` is blank --
      the load-bearing anti-forgery check. Those codes are asserted only in
      `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs`, never
      across REST -> gateway -> `/process`. `Unavailable`/`Stale` map to a retryable 503, a materially
      different caller contract than the non-retryable 403 this bundle pins, so a regression that
      collapsed one into the other would keep every row green. Adding those rows is outside this
      intent's four-scenario matrix.
    location: >-
      tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs
    severity: medium
  - summary: >-
      `HasScopedMetadataProperty` rejects any property name containing `policy`, which production's
      own richer safe-denial body carries.
    evidence: |-
      `FolderAuthorizationDenialMapper.ToHttpResult` emits `details.policyClass` (alongside
      `reasonCategory`, `layer`, `freshnessClass`, `timingBucket`) on every layered-authorization
      denial. The helper passes today only because the gateway double flattens the `/process` 403 into
      an `EventStoreGatewayException`, so the caller sees `ToArchiveGatewayProblem`'s leaner
      `SafeProblem` body instead. Any future change that propagates the richer -- and still
      metadata-only -- denial body to the caller would red these rows on a body that discloses
      nothing. Closing this means matching exact field names, or exempting the known-safe classifier
      fields, rather than substring-matching property names.
    location: >-
      tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Archive integration coverage does not prove mid-processor cancellation cleanup, same-key equivalent replay evidence, foreign envelope-tenant rejection, or policy denial across the real REST -> gateway -> `/process` boundary. The missing rows leave persisted state and forbidden-side-effect invariants unverified.

**Approach:** Extend only the in-process integration harness needed to drive those conditions, then add four archive tests that assert caller-visible responses, traversal evidence, persisted folder state, append counts, and cleanup/no-side-effect outcomes.

## Boundaries & Constraints

**Always:** Drive requests through the public archive REST endpoint and the real in-process gateway, `/process` handler, processor, and applicable authorization/gate layers; assert tenant-scoped persisted end state; preserve metadata-only safe denials; propagate successful `/process` result payloads faithfully; use `TestContext.Current.CancellationToken` except where a test-owned token deliberately triggers the cancellation scenario.

**Block If:** Completion requires changing the `Hexalith.EventStore` `IDomainProcessor` contract, changing production archive policy semantics, or choosing a caller-visible denial category not already selected by current production mappings.

**Never:** Edit the deferred-work ledger; initialize nested submodules; add provider/network/credential dependencies; weaken authorization ordering; claim that the public request token propagates inside `IDomainProcessor`; hand-edit generated clients; append an event on cancellation, tenant mismatch, or policy denial.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Mid-processor cancellation | Authorized active folder; policy fake signals processor entry then triggers test-owned cancellation | Gateway and `/process` are reached; request is canceled; authorization scope is cleared; folder remains active; zero events append | `OperationCanceledException` is not converted to a domain rejection or safe 500 |
| Equivalent replay | Same tenant/folder/key/body/correlation/task submitted twice | Both responses are 202; second reports `idempotentReplay: true`; two process calls; exactly one archive event; folder remains archived with original evidence | No duplicate append or idempotency conflict |
| Foreign envelope tenant | REST authenticates tenant-a while test gateway emits tenant-b in `/process` envelope | Safe 403 before processor/policy/repository observation; one process round-trip; active state and zero appends | No tenant or folder existence disclosure |
| Policy denied | Authorized active folder with denying archive-policy provider | Safe 403 after processor reaches policy evidence; provider called once for tenant-a/org-a/folder-a; active state and zero appends | Preserve current generic safe-denial mapping |

</intent-contract>

## Code Map

- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` -- existing archive round-trip suite; add four tests and narrow `StartHostAsync` injection seams near the current cancellation test and host factory.
- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` -- shared real `/process` test hop; currently drops `DomainServiceWireResult.ResultPayload` and always copies the submitted tenant. Preserve cloned JSON result payload and add an optional envelope-tenant transform whose default is unchanged.
- `tests/Hexalith.Folders.IntegrationTests/DenyingFolderArchivePolicyEvidenceProvider.cs` -- new test-only fake returning scoped `FolderArchivePolicyEvidence.Denied` and recording safe call evidence.
- `tests/Hexalith.Folders.IntegrationTests/CancelMidFlightFolderArchivePolicyEvidenceProvider.cs` -- new controllable test-only fake that signals entry, triggers test-owned cancellation, and exposes completion so cleanup assertions do not race server unwind.
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs` -- read-only evidence: layered authorization compares the envelope tenant before processor dispatch and clears the scoped accessor in `finally`.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` -- read-only evidence: archive calls ACL/policy providers, preserves `OperationCanceledException`, and emits replay in the successful no-op result payload.
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs` -- read-only evidence: policy runs before stream access, equivalent replay avoids append, and denials leave state unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` -- faithfully return successful `/process` result payloads and support an opt-in envelope-tenant transform without changing existing callers.
- [x] `tests/Hexalith.Folders.IntegrationTests/DenyingFolderArchivePolicyEvidenceProvider.cs` and `CancelMidFlightFolderArchivePolicyEvidenceProvider.cs` -- add deterministic, cancellation-aware test fakes with metadata-only observations.
- [x] `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` -- add the four named bundle tests, inject optional test dependencies through `StartHostAsync`, and assert response JSON/status, process/provider calls, authorization cleanup, append count, and loaded lifecycle state.

**Acceptance Criteria:**
- Given the four matrix scenarios, when the focused integration tests execute, then each observes the public REST response and the persisted or unchanged folder end state through the real in-process process wiring.
- Given cancellation or a denial, when processing terminates, then no archive event is appended and no stale scoped authorization evidence survives.
- Given an equivalent replay, when the second response is serialized, then replay evidence is true and the original single archived transition remains authoritative.
- Given existing integration callers use the shared gateway without overrides, when the project test suite runs, then their envelope tenant behavior is unchanged and successful result payloads are preserved rather than discarded.

## Spec Change Log

## Review Triage Log

### 2026-08-28 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 1, medium 5, low 3)
- defer: 1: (high 0, medium 1, low 0)
- reject: 14: (high 0, medium 5, low 9)
- addressed_findings:
  - `[high]` `[patch]` Recorded server-side propagation of `OperationCanceledException` separately from client cancellation.
  - `[medium]` `[patch]` Retried the canceled command with the same idempotency key and proved a single archive append.
  - `[medium]` `[patch]` Proved the host and scoped authorization handoff remain usable after cancellation unwind.
  - `[medium]` `[patch]` Added explicit scoped-authorization cleanup evidence for policy denial.
  - `[medium]` `[patch]` Expanded both safe-denial tests to assert retryability, client action, metadata-only visibility, and absence of scoped resource fields.
  - `[low]` `[patch]` Expanded replay assertions to cover status, correlation ID, and task ID.
  - `[low]` `[patch]` Moved the process-call increment after tenant transformation so the counter cannot claim an unsent round-trip.
  - `[low]` `[patch]` Disposed all newly introduced HTTP responses.
  - `[medium]` `[patch]` Renamed the DW-17 and DW-18 tests to their exact ledger-requested identities.

### 2026-08-28 — Review pass

- intent_gap: 0
- bad_spec: 0
- patch: 8: (high 0, medium 3, low 5)
- defer: 1: (high 0, medium 1, low 0)
- reject: 23: (high 0, medium 7, low 16)
- addressed_findings:
  - `[medium]` `[patch]` `AssertMetadataOnlySafeDenial` scanned property *names* only, so a scoped
    identifier interpolated into a free-text `message`/`title`/`detail`/`type` value passed the
    invariant the helper is named for. Added `FindDisclosedValue`, a string-value scan over the
    seeded `tenant-a`/`tenant-b`/`org-a`/`folder-a`/`v1-test-denied` identifiers. Proven
    non-vacuous by a negative control: adding a value the body actually carries (`denied_safe`) to
    the forbidden list reddens both denial rows.
  - `[medium]` `[patch]` The three cancellation-handshake `WaitAsync` calls had no deadline and the
    middleware matched `/process` with an exact ordinal string compare, so a route change or a
    swallowed `OperationCanceledException` would hang the run instead of failing the row. Added a
    30-second `SignalTimeout` to every wait and switched to
    `Path.StartsWithSegments("/process", OrdinalIgnoreCase)`.
  - `[medium]` `[patch]` The envelope-tenant row asserted no scoped-authorization cleanup, leaving
    the "no stale scoped authorization evidence survives" criterion proven only for the policy-denial
    path. Injected the accessor and asserted `Current` is null after the pre-processor denial.
  - `[low]` `[patch]` The post-cancellation retry never inspected its body, so nothing proved the
    cancelled attempt left no idempotency ledger entry for the same key. Added
    `idempotentReplay == false` on the retry response.
  - `[low]` `[patch]` The cancel fake returned a divergent `policyVersion` on the retry, and that
    value is bound into the archive decision fingerprint by `FolderArchiveTenantGate`. Pinned a
    single constant so a same-key retry cannot fail as a conflict for a reason unrelated to the
    unwind under test.
  - `[low]` `[patch]` Neither fake nor the test recorded that `IDomainProcessor.ProcessAsync` carries
    no token, so the `ThrowIfCancellationRequested` guards read as live propagation evidence. Added
    remarks to both fakes stating the guards are defensive and the cancellation is manufactured.
  - `[low]` `[patch]` `AddSingleton(archivePolicyEvidenceProvider)` inferred its service type, so a
    future narrowing of the parameter would silently restore the allow-everything baseline provider
    and turn both denial rows green for the wrong reason. Registered explicitly against
    `IFolderArchivePolicyEvidenceProvider`, and documented why the authorization accessor's singleton
    lifetime deliberately diverges from production's `TryAddScoped`.
  - `[low]` `[patch]` `InProcessRejectionPropagatingGatewayClient`'s `<remarks>` still described the
    pre-change behavior. Documented the result-payload propagation (including the two known fidelity
    gaps: echoed `MessageId`, no oversized-payload guard) and the opt-in envelope-tenant seam.

### 2026-08-28 — Review pass

- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 3, low 2)
- defer: 2: (high 0, medium 2, low 0)
- reject: 27: (high 0, medium 7, low 20)
- addressed_findings:
  - `[medium]` `[patch]` `ForbiddenDenialDisclosures` omitted the seeded acting principal, so an
    actor identity interpolated into a free-text denial value passed the value scan added last
    pass. Added `user-a`. The idempotency key is deliberately still absent: the safe denial
    legitimately carries the key-derived `correlationId`, and listing it would red both rows on a
    body that discloses nothing.
  - `[medium]` `[patch]` `AssertMetadataOnlySafeDenial` proved only what the denial must *not*
    contain, never that it stays correlatable. A regression dropping `correlationId` from safe
    denials -- making them untriageable for operators -- would have gone green, and both scans would
    have passed vacuously over a truncated body. Added
    `correlationId == "correlation-archive-key-a"`.
  - `[medium]` `[patch]` The shared double's new result-payload propagation made `idempotentReplay`
    observable for all 13 mutation endpoints, but the bundle asserted it on one. The flag is written
    by two different payload builders, and `FolderDomainProcessor.OrganizationAcceptedNoOp`'s
    `AlreadyApplied` branch was pinned nowhere -- `ConfigureProviderBindingReplayWithSameKeyShouldNotPersistTwice`,
    in this same class, drives that exact path but read only status codes and the append counter.
    Added `false`-then-`true` body assertions there, closing AC4's "successful result payloads are
    preserved rather than discarded" for an existing caller. Negative control: hardcoding
    `IdempotentReplay: false` in `FolderDomainProcessor` reddens that row; reverted.
  - `[low]` `[patch]` `authorizationAccessor.Current.ShouldBeNull()` on the envelope-tenant row
    cannot fail: that denial returns at `FoldersDomainServiceRequestHandler.cs:87`, before
    `BeginScope` at line 108. It reads as begin-then-clear evidence but is a leak guard on a
    pre-scope early return. Documented what it does and does not prove, and where the real
    begin-then-clear chain lives (the policy row's `Calls == 1` plus `Current is null`).
  - `[low]` `[patch]` The replay row passed `CreateValidArchiveRequest(...)` inline to `SendAsync`
    twice, leaking both `HttpRequestMessage` instances against the convention every sibling row in
    the file follows. Hoisted both into `using` locals.

## Design Notes

The ADR intentionally leaves `IDomainProcessor` without request-token propagation. The cancellation test therefore uses a controlled in-processor policy fake that both cancels the client token and throws cancellation after proving entry; it must wait for server cleanup completion before inspecting state. Foreign-tenant injection belongs in the test gateway because the public endpoint correctly derives its tenant from authenticated context.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore` -- expected: all integration tests pass, including the four new archive rows.
- `dotnet build Hexalith.Folders.slnx --no-restore` -- expected: solution builds with warnings treated as errors.
- `git diff --check` -- expected: no whitespace errors or conflict markers.

## Auto Run Result

Status: done
Blocking condition: none

### Implemented change

Third review pass over the archive process-integration bundle (baseline `1618b846cbcddaf7ad69dfefbcbe34cd63df2ee1`). The four archive rows and the harness seams were implemented in `a4e5e5b` and hardened in `7a76f2e`; this pass reviewed the accumulated diff across four layers and applied five patches. Triage produced zero `intent_gap` and zero `bad_spec` findings, so no spec amendment and no implementation loopback occurred and `review_loop_iteration` stays at 0.

The patches close three evidence gaps and two honesty gaps. The safe-denial helper now also proves the denial stays *correlatable* and refuses to disclose the acting principal; the `idempotentReplay` flag that the shared-double change made observable is now pinned on a second, independently-written payload builder; and the one assertion that could never fail is labelled as the leak guard it actually is.

### Files changed

- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` — four archive rows (cancellation unwind, equivalent replay, foreign envelope tenant, policy denial) plus the `StartHostAsync` injection seams. This pass added `user-a` to the disclosure scan, the `correlationId` assertion in `AssertMetadataOnlySafeDenial`, `idempotentReplay` assertions on the existing provider-binding replay row, the vacuity note on the envelope-tenant accessor assertion, and `using` locals for the replay row's two requests.
- `tests/Hexalith.Folders.IntegrationTests/CancelMidFlightFolderArchivePolicyEvidenceProvider.cs` — controllable cancel-mid-flight policy fake (unchanged this pass).
- `tests/Hexalith.Folders.IntegrationTests/DenyingFolderArchivePolicyEvidenceProvider.cs` — scoped denying policy fake (unchanged this pass).
- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` — result-payload propagation and the opt-in envelope-tenant seam (unchanged this pass).

### Review findings breakdown

- Patches applied: 5 (medium 3, low 2).
- Items deferred: 2 (medium 2) — no integration row for the retryable archive-policy outcomes (`Unavailable`/`Stale` map to 503, a different caller contract than the 403 pinned here), and `HasScopedMetadataProperty`'s substring match on `policy` which would red against production's own richer, still metadata-only denial body.
- Items rejected: 27 (medium 7, low 20) — chiefly scenarios outside the intent's four-row matrix (post-append cancellation, seeded-foreign-tenant smuggling, fresh-correlation-id replay, the four uncovered `FolderArchivePolicyOutcome` values as *patches* rather than deferrals), category choices the intent's Block-If forbids (routing the policy denial to `policy_denied`), speculative hardening of the shared double, and cosmetic placement/naming preferences.

### Follow-up review recommendation

`true`. Patched this pass: high 0, medium 3, low 2. Score = 3 x 3 + 1 x 2 = 11, at or above the threshold of 5.

### Verification performed

- `dotnet build Hexalith.Folders.slnx` — Build succeeded, 0 warnings, 0 errors (warnings-as-errors in force).
- `dotnet test tests/Hexalith.Folders.IntegrationTests/...` — 634 passed, 0 failed, 0 skipped.
- Parity-consuming lanes that share `tests/shared/Parity`: Client 288/0, Cli 708/0, Server 575/0, Mcp 662/0 — all passed.
- Negative control on the new provider-binding assertion: hardcoding `IdempotentReplay: false` in `FolderDomainProcessor.OrganizationAcceptedNoOp` reddened `ConfigureProviderBindingReplayWithSameKeyShouldNotPersistTwice`, confirming the assertion is live and that the branch was previously unpinned. Production file restored and re-verified clean via `git status src/`.
- `dotnet format whitespace` scoped to the changed project — clean.
- `git diff --check` — clean.

### Residual risks

- **The four rows still run in no blocking CI lane** (deferred item 1, ledger DW-305). `run-baseline-ci-gates.ps1` omits the IntegrationTests project and `run-contract-parity-ci-gates.ps1` selects only three other classes, so every invariant proven here can regress on main without a red build. Pre-existing selection gap; it caps the bundle's value at local evidence. This pass narrowed but did not close deferred item 2 (DW-306): the shared double's result-payload propagation is now asserted by two rows instead of one, but both live in the unselected class.
- **The cancellation row cannot prove token propagation, by design.** `FolderDomainProcessor` hands evidence providers `CancellationToken.None` because `IDomainProcessor.ProcessAsync` carries no token. The row proves that an `OperationCanceledException` escapes `/process` unconverted, that `EndScope` ran, that nothing appends, and that the host stays usable — not that a client abort reaches the processor. The fake's remarks say so; the method name still reads broader than the evidence.
- **Both 403 rows land on the same generic `tenant_access_denied` / `denied_safe` fallback,** so their bodies are indistinguishable; only `policyProvider.Calls` (0 vs 1) discriminates an envelope-tenant denial from a policy denial. This preserves the current production mapping as the intent requires, but a regression collapsing one into the other would keep both rows green. `FolderCanonicalErrorMapper` does define `policy_denied`, so the divergence is real but out of scope here.
- **The envelope-tenant row's caller-visible 403 shape is partly an artifact of the double,** which converts any non-2xx `/process` response into a status-preserving `EventStoreGatewayException`. The deployed path routes a non-2xx domain-service response through `DaprDomainServiceInvoker` as a transport failure instead, so the production caller would not see this body. Pre-existing pass-through behavior that the new rows inherit rather than introduce.
- **The policy-denied row has no live production counterpart.** `BaselineFolderArchivePolicyEvidenceProvider` returns `Allowed` unconditionally, so the row proves gate and HTTP-mapping plumbing under a fake, consistent with DW-19's own deferral text.
- **The authorization accessor is registered as a singleton in the harness** where production uses `TryAddScoped`. That override is what makes `Current` observable across requests; it means the assertions prove `EndScope` ran, not the per-request isolation production gets from the scoped lifetime. Documented inline.
