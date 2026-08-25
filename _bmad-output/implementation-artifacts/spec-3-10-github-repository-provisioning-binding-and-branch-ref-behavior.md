---
title: 'Story 3.10: GitHub repository provisioning, binding, and branch/ref behavior — durable idempotency admission'
type: 'feature'
created: '2026-08-25'
status: 'done'
baseline_revision: '4fd2526dc3c3163639fd14ec5af6248af26cfcbe'
baseline_commit: '4fd2526dc3c3163639fd14ec5af6248af26cfcbe'
review_loop_iteration: 0
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.10's GitHub create/bind slice is implemented except AC7: the provider accepts a raw `IdempotencyKey` string and never gates on it, so a replayed, conflicting, or expired intent still resolves the target, leases a credential, builds a client, and calls GitHub. The story HALTed twice waiting for Story 12.6 to own durable expiry storage, which remains blocked on unreleased EventStore work.

**Approach:** Adopt the caller-supplied `ProviderIdempotencyAdmission` seam that Story 3.11 already landed for file mutation and commit. The adapter enforces the durable admission decision before any target, credential, client, or provider access; producing that decision stays with Story 12.6. Also repair the pre-existing `CS8122` compile break that currently makes the whole `Hexalith.Folders.Tests` lane unbuildable.

## Boundaries & Constraints

**Always:** Evaluate the admission after boundary/evidence validation and credential-mode plus safe-target-fingerprint checks, and strictly before target resolution, credential resolution, client construction, and any GitHub call — mirroring `GitHubProvider.Mutations.ReplayOrReject`. Reuse the existing `ProviderIdempotencyDisposition`, `ProviderFailureCategory`, and `idempotency_conflict` / `idempotency_key_expired` reason codes. Replay returns the prior safe outcome fingerprint with no provider access and no prior-intent disclosure. Keep every result, log, and exception metadata-only.

**Ask First:** Any change to the OpenAPI Contract Spine, generated SDK, parity fixtures, or C13 inventory; any change to `sprint-status.yaml`; any attempt to make Folders own durable admission storage, key retention, or tombstones.

**Never:** Invent durable expiry/retention storage, consumed-key tombstones, or an admission coordinator inside Folders — Story 12.6 and EventStore own that. Never implement Forgejo gating (Story 3.12), GitHub readiness (Story 3.3), file/commit/status behavior (Story 3.11), or Story 3.14's runtime subscription and final folder transition. Never repair the unrelated pre-existing red rows in `Hexalith.Folders.Contracts.Tests` (NFR traceability, governance digest). Never hand-edit generated clients.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Fresh admission | `Disposition = Fresh` | Execution continues to target resolution and the single GitHub create/bind call | Unchanged existing failure mapping |
| Equivalent replay | `Disposition = EquivalentReplay` carrying a prior safe outcome fingerprint | Success with `EquivalentExisting = true` and the deterministic safe target fingerprint; zero target, credential, client, and provider calls | A blank prior outcome fingerprint is a validation failure, never a provider call |
| Conflicting key | `Disposition = Conflict` | Failure `ProviderConflict` / `idempotency_conflict` before provider access, disclosing nothing about the prior intent | N/A |
| Expired key | `Disposition = Expired` | Failure `ProviderConflict` / `idempotency_key_expired`; never executes as new work | N/A |
| Denied or stale evidence | Boundary/evidence check fails | Existing denial result | Denial still precedes the admission check; zero provider and secret-store calls |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Mutations.cs:683-725` -- `ReplayOrReject` for change-set and commit. The exact pattern to mirror; call site at `:49` shows the required ordering (after safe-target fingerprint, before resolver/credential/client).
- `src/Hexalith.Folders/Providers/Abstractions/ProviderIdempotencyAdmission.cs`, `ProviderIdempotencyDisposition.cs` -- reuse as-is; no new types. `ProviderFileChangeSetRequest.cs` / `ProviderCommitRequest.cs` show where the admission sits on a request record.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs` -- add the admission **before** the trailing defaulted `RepositoryProfileRef`.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingRequest.cs` -- append the admission.
- `ProviderRepositoryCreationResult.cs:18`, `ProviderRepositoryBindingResult.cs:18` -- existing `Success(request, equivalentExisting, safeTargetFingerprint)` / `Failure(...)` already cover replay and rejection; no new factory needed.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs:170` (`CreateRepositoryAsync`) and `:321` (`ValidateRepositoryBindingAsync`) -- insert the gate immediately after the `GitHubSafeTargetFingerprint.TryCreate` block and before `_targetResolver` / `_credentialResolver` / `_apiClientFactory`.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:66` -- only positional construction site; `RepositoryBindingRequested` (`IdempotencyKey`, `IdempotencyFingerprint`) supplies the intent fingerprint.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs` -- carry an optional caller-supplied admission so Story 12.6 can inject the durable decision later.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:172` -- binding construction site; `validation.IdempotencyFingerprint` is in scope.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs:623` -- **pre-existing `CS8122`**: `ShouldNotContain` takes an expression tree, so `request.Method is not null` is illegal. This breaks the entire test project.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs:1588` (`CreationRequest`), `:1614` (`BindingRequest`), `:926` -- named-argument factories plus the existing `ProviderIdempotencyDisposition` → reason-code theory to extend.
- `tests/.../GitHub/RecordingProviderRepositoryTargetResolver.cs`, `RecordingGitHubCredentialResolver.cs`, `RecordingGitHubHttpMessageHandler.cs` -- existing recorders that prove zero-touch.
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs:451`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs:792` -- factories that must keep compiling.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs` -- replace the `is not null` pattern inside the `ShouldNotContain` expression tree with `!= null` -- unblocks the baseline build before any other work.
- [x] `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs`, `ProviderRepositoryBindingRequest.cs` -- add `ProviderIdempotencyAdmission IdempotencyAdmission` -- makes the durable decision an explicit caller input.
- [x] `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs` -- add `ReplayOrReject` overloads for creation and binding and call them at the ordering point above -- closes AC7 at the adapter boundary.
- [x] `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs`, `RepositoryProvisioningProcessManager.cs` -- accept an optional admission and default to `Fresh` with `requested.IdempotencyFingerprint` -- preserves today's ledger-based dedup while exposing the seam for Story 12.6.
- [x] `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- supply `Fresh` with `validation.IdempotencyFingerprint` -- keeps the binding path compiling and honest.
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs` -- cover every I/O Matrix row for both create and bind, asserting recorder call counts are zero on replay/conflict/expiry -- proves ordering, not just mapping.
- [x] `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs` -- update factories and assert the forwarded admission -- keeps sibling lanes green.
- [x] `_bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md` -- tick the completed tasks, append a Change Log row, and record the AC7 resolution plus surviving out-of-scope reds -- keeps the story file the authoritative record.

**Acceptance Criteria:**
- Given a denied, stale, or malformed request, when create or bind runs, then denial still precedes the admission check and no provider, resolver, or secret-store call occurs.
- Given `EquivalentReplay`, when create or bind runs, then the prior safe outcome is returned and the recording target resolver, credential resolver, and HTTP handler each record zero calls.
- Given `Conflict` or `Expired`, when create or bind runs, then the canonical reason code is returned with no provider access and no prior-intent disclosure.
- Given any admission outcome, when results, logs, or exceptions are inspected, then no token, owner, repository, ref, URL, or provider body appears.

## Design Notes

The admission is a **decision carried in**, not a decision made here — identical to Story 3.11. That is what lets AC7 close without Folders owning retention, tombstones, or expiry clocks, which OQ8 and Story 12.6 explicitly reserve.

```csharp
private static ProviderRepositoryCreationResult? ReplayOrReject(
    ProviderRepositoryCreationRequest request,
    string safeTargetFingerprint)
    => request.IdempotencyAdmission.Disposition switch
    {
        ProviderIdempotencyDisposition.Fresh => null,
        ProviderIdempotencyDisposition.EquivalentReplay when
            !string.IsNullOrWhiteSpace(request.IdempotencyAdmission.PriorSafeOutcomeFingerprint)
            => ProviderRepositoryCreationResult.Success(request, equivalentExisting: true, safeTargetFingerprint),
        ProviderIdempotencyDisposition.Conflict => ProviderRepositoryCreationResult.Failure(
            request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
        ProviderIdempotencyDisposition.EquivalentReplay => ProviderRepositoryCreationResult.Failure(
            request, ProviderFailureCategory.ProviderValidationFailed, "idempotency_replay_evidence_missing"),
        _ => ProviderRepositoryCreationResult.Failure(
            request, ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
    };
```

Forgejo keeps ignoring the new member; Story 3.12 wires it.

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx` -- expected: 0 errors, 0 warnings (baseline is currently red with `CS8122`).
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-build` -- expected: all green, including the new admission tests.
- `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj --no-build` -- expected: all green.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build` -- expected: no NEW failures; report the pre-existing NFR-traceability and governance-digest rows as out of scope.
- `git diff --check` -- expected: clean.

## Suggested Review Order

**The gate and where it sits**

- Entry point: the admission check lands after the safe-target fingerprint, before every resolver.
  [`GitHubProvider.cs:202`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L202)

- Same ordering on the binding path, so read-only observation is gated too.
  [`GitHubProvider.cs:361`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L361)

- The disposition switch itself; replay evidence is already validated by this point.
  [`GitHubProvider.cs:541`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L541)

**Malformed-admission parity with Story 3.11 (added in review)**

- Null, undefined enum, and unsafe intent fingerprint become canonical failures, not an NRE.
  [`GitHubProvider.cs:524`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L524)

- Replay evidence must be a safe fingerprint before a prior outcome is trusted.
  [`GitHubProvider.cs:532`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L532)

- Wired into the creation boundary, reusing the existing Story 3.11 reason codes.
  [`GitHubProvider.cs:607`](../../src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs#L607)

**Contract change**

- The admission sits before the trailing defaulted member so callers must decide.
  [`ProviderRepositoryCreationRequest.cs:16`](../../src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs#L16)

- Binding request carries the same member.
  [`ProviderRepositoryBindingRequest.cs:19`](../../src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingRequest.cs#L19)

**Production callers — both deliberately Fresh until Story 12.6**

- Optional so Story 12.6 can inject the durable decision; fail-open risk tracked as DW-295.
  [`RepositoryProvisioningContext.cs:15`](../../src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs#L15)

- The only caller that reaches the GitHub gate today; comment names the future producer.
  [`RepositoryBindingService.cs:193`](../../src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs#L193)

**Verification**

- Closes the review's main gap: the forwarded admission is now asserted, not assumed.
  [`FolderRepositoryBindingGateTests.cs:344`](../../tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBindingGateTests.cs#L344)

- Capture seam that made that assertion possible.
  [`RecordingProviderCapabilityResolver.cs:57`](../../src/Hexalith.Folders.Testing/Providers/RecordingProviderCapabilityResolver.cs#L57)

- Conflict and expiry rejected with zero resolver, credential, and client calls.
  [`GitHubProviderTests.cs:1591`](../../tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs#L1591)

- Undefined disposition is a malformed admission, no longer silently "expired".
  [`GitHubProviderTests.cs:1865`](../../tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs#L1865)

- One-line baseline repair that made the whole test project buildable again.
  [`OctokitGitHubApiClientTests.cs:623`](../../tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs#L623)
