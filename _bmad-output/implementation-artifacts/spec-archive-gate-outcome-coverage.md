---
title: 'Cover archive gate policy ACL and append outcomes'
type: 'refactor'
created: '2026-08-29'
status: 'done'
baseline_revision: 'fbf4b6832eadee9eb6747bd163693787460b0cc7'
baseline_commit: 'fbf4b6832eadee9eb6747bd163693787460b0cc7'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** `FolderArchiveTenantGate` has untested result mappings for `FolderArchivePolicyOutcome.ScopeMismatch`, six non-allowed ACL outcomes, and a race-time `FolderAppendOutcome.FingerprintConflict`. These gaps leave fail-closed result codes and resource-touch ordering vulnerable to silent regression.

**Approach:** Extend the focused archive gate unit suites and their recording repository to drive each missing outcome, then assert the safe code, empty result events, and exact absence of unintended stream loads or durable appends.

## Boundaries & Constraints

**Always:** Exercise the public `FolderArchiveTenantGate.Handle` surface; cover every currently untested declared `FolderArchiveAclOutcome`; use the existing `RecordingFolderRepository` counters; preserve ordinal tenant/folder/principal scoping and metadata-only assertions; keep tests hermetic.

**Block If:** A requested outcome cannot be reached without changing production behavior or public contracts, or the declared outcome sets no longer match the bundle's named branches.

**Never:** Edit the deferred-work ledger; change production gate mappings; weaken safe-denial ordering; hand-edit generated files; initialize nested submodules; broaden into REST or integration coverage already owned by Story 2.8.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Policy scope mismatch | Allowed tenant and ACL evidence plus `ScopeMismatch` policy evidence | `PolicyEvidenceScopeMismatch`; no events, stream construction, lookup, load, or append | Fail closed before folder observation |
| ACL evidence rejection | Each of `Unavailable`, `Malformed`, `Stale`, `TenantMismatch`, `FolderMismatch`, and `UnsupportedAction` | Existing canonical mapped code; no events, stream construction, lookup, load, or append | Fail closed before policy/folder observation |
| Append fingerprint race | Allowed evidence, existing active folder, missing preflight key, append returns `FingerprintConflict` | `IdempotencyConflict`; one initial load and append attempt, zero appended events, no conflict reread | Preserve state and durable-key absence |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs:73-140,203-247` -- read-only behavior under test: ACL precedes policy and all repository observation; append outcome maps `FingerprintConflict` to `IdempotencyConflict` without reread.
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveAclOutcome.cs:3-13` -- read-only exhaustive ACL outcome set; `Allowed` and `Denied` already have gate coverage, leaving six rejection rows.
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchivePolicyOutcome.cs:3-11` -- read-only policy outcome set; `ScopeMismatch` is the only declared rejection absent from the existing policy theory.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs:63-163` -- extend existing ACL/policy mapping tests and reuse their safe pre-observation counter assertions.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs:109-186` -- add a focused append-return mapping test beside current append-conflict race tests.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs:33-96` -- add a one-purpose fingerprint-conflict simulation that increments the append-attempt counter but never mutates state, durable keys, or appended-event evidence.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs` -- expose deterministic append-time fingerprint-conflict simulation without changing default behavior.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs` -- add exhaustive missing ACL rows and the policy scope-mismatch row, asserting canonical codes plus zero observation/append counters.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs` -- drive the simulated append result through `FolderArchiveTenantGate` and assert conflict mapping with no durable append or extra load.

**Acceptance Criteria:**
- Given each currently untested ACL rejection outcome, when `FolderArchiveTenantGate.Handle` runs, then it returns the established safe result code with no events and no stream construction, idempotency lookup, stream load, or append.
- Given scope-mismatched archive policy evidence, when the gate runs, then it returns `PolicyEvidenceScopeMismatch` with no events and no repository observation or append.
- Given an eligible active folder whose append atomically reports `FingerprintConflict`, when the gate runs, then it returns `IdempotencyConflict`, performs only the expected initial stream load and append attempt, and records no appended event or durable key.

## Spec Change Log

## Review Triage Log

### 2026-08-29 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 0
- defer: 0
- reject: 18: (high 0, medium 0, low 18)
- addressed_findings:
  - none

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -m:1 -v:m` -- expected: zero warnings and errors.
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -noLogo -parallel none -class Hexalith.Folders.Tests.Aggregates.Folder.FolderArchiveAuthorizationGateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderArchiveIdempotencyTests` -- expected: both focused classes pass.
- `dotnet format whitespace tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --verify-no-changes --include tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs` -- expected: no formatting changes required.
- `git diff --check -- tests/Hexalith.Folders.Tests/Aggregates/Folder` -- expected: no whitespace errors.

## Auto Run Result

Status: done

### Implemented change

Added focused `FolderArchiveTenantGate` coverage for every previously untested ACL rejection outcome, policy `ScopeMismatch`, and append-time `FingerprintConflict`. The tests pin canonical safe result codes, empty rejection events, fail-closed pre-observation counters, and the absence of a durable append or conflict reread.

### Files changed

- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs` -- added deterministic append-time fingerprint-conflict simulation without state or ledger mutation.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs` -- covered six ACL outcomes and the missing policy scope-mismatch row with no-touch assertions.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs` -- covered the append-result mapping to `IdempotencyConflict` and asserted one initial load, no reread, and no durable append.
- `_bmad-output/implementation-artifacts/spec-archive-gate-outcome-coverage.md` -- recorded the plan, review triage, verification, and completion result.

### Review findings

- Patches applied: 0.
- Items deferred: 0.
- Items rejected: 18 low-severity suggestions that tested broader combinations, future enum drift, invalid enum values, or production-pipeline behavior outside the bundle's focused current-outcome coverage.

### Follow-up review recommendation

`false` -- patched findings: high 0, medium 0, low 0; score `3 × 0 + 1 × 0 = 0`.

### Verification performed

- Test-project build passed with 0 warnings and 0 errors.
- Both focused archive test classes passed: 41 total, 0 failed, 0 skipped.
- The three matrix-covering methods passed directly: 11 total theory/fact cases, 0 failed, 0 skipped.
- `dotnet format whitespace --verify-no-changes` passed.
- `git diff --check` passed.
- The deferred-work ledger remained unchanged.

### Residual risks

None within the deferred bundle's focused gate-outcome scope. Production archive-path integration coverage remains owned by the existing Story 2.8 suites.
