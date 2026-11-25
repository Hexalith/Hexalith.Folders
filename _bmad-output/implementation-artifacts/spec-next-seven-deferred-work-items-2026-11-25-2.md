---
title: 'Complete the next seven eligible deferred Folders items'
type: 'chore'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the next seven eligible items from the deferred-work ledger in priority order. Add applicable tests, mark each item done and commit it separately. Record blocked or obsolete entries and continue in order. Run the full local build and test suite, address failures, and push the verified commits to `main`.

</frozen-after-approval>

## Implementation Notes

Use the established priority rule: explicit high, then medium, then untagged entries in ledger order, then low. Start the untagged scan after the last item completed by the previous batch.

1. Canonicalize Forgejo object IDs at the ref observation boundary and in commit confirmation. Preserve equivalent legacy uppercase inputs.
2. Measure serialized tenant-access history growth and compare it with the documented Dapr transport ceiling.
3. Add the historical Story 12.1 focused build and test commands to the completion-reconciliation record.
4. Add a fresh-checkout restore prerequisite to the current-dependency verification record.
5. Add reproducible Platform and Tenants comparison commands to that record.
6. Correct the old tenant topic and publisher in the project context.
7. Align the shared production pub/sub policy with the two documented EventStore dead-letter topics and verify the exact scopes.

### Entries skipped

Ledger line numbers refer to `c419887fcf508576055488578eacd22e910da302`. Earlier untagged skips remain recorded in the [previous batch](spec-next-seven-deferred-work-items-2026-11-25.md#skipped-entries).

| Entry | Reason |
| --- | --- |
| DW-329 and DW-344 | The active CI workflow lists the Server test project. The earlier absence is obsolete. |
| DW-330 | The production file cited by the old formatting failure conforms to the current whitespace rules. |
| DW-317 | The approved OpenAPI spine and status mapping require a governed revision. |
| DW-319 | Production callers now use both canonical mapping methods. |
| DW-343 | The affected GitHub replay fixtures now supply the required repository identity. |
| L2845 | The publication preflight already rejects malformed success responses. |
| L2855 | Story 11.15 is still in backlog. A prepared composite opts into the full AppHost project, while the active domain workflow does not invoke it. Activation belongs to CI lane ownership. |
| L2859 and L2863 | Story 3.13 deliberately classifies read-only advertisement cancellation before mutation dispatch and retains the native permit until cleanup. The current transport tests cover those decisions. |
| L2871, L2879, L2887, and L2891 | Installation authority, erased owners, and named dispatch belong to EventStore. Folders uses a separate projection path. |
| L2875 and L2883 | The EventStore producer now handles absent zero-event owners and reads long owner streams in pages. |
| L2895 | Lower-sequence handling is part of the approved tenant-access contract. Changing it safely requires a recovery design for already persisted rows and filtered event streams. |
| L2908 | Resolving non-user grants requires authoritative group, role, and delegation evidence and a governed authorization update. |
| L2909 | The production reader now uses the EventStore native metadata SDK. The Dapr actor-body question no longer applies. |
| L2913 and L2916 | Receipt clock-skew policy and a trusted cached-replay disposition belong to EventStore. |
| L2929 and L2930 | Effective action levels and the hash-pinned G4 matrix require owner approval. |
| L2931 | Isolating the idempotency adapter from the domain assembly needs an architecture decision about contract and validation ownership. |

## Review Triage Log

- Medium, fixed. The follow-up capacity probe used `EventStoreFolderTenantAccessProjectionStore` and the platform SDK's conditional write path. It verified readback before and after the rejected write.
- Low, fixed. The ledger records the fixture type, dependency versions, row construction, size and readback. A runnable disposable probe and its output are retained with the local verification logs.
- Medium, deferred. Production traffic and effective deployment limits were unavailable to the synthetic probe. The ledger carries a separate production headroom item.
- Medium, fixed. The rejected Forgejo commit test now supplies uppercase native evidence and requires a lowercase returned commit ID.
- Medium, fixed. The integrated Forgejo smoke fixture submits a legacy uppercase expected head to native commit negotiation and verifies the resulting commit and ref.
- Medium, fixed. The Dapr policy regression checks that optional component and topic allow lists permit EventStore to reach both protected topics.
- Low, fixed. Both historical build commands now select the same source graph as the restore command, including under `CI=true`.
- Low, fixed. The Dapr promotion handoff lists both new EventStore publication scopes.
- Low, fixed. The batch record includes the reasons for skipping earlier entries.

## Verification

- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` and `dotnet build Hexalith.Folders.slnx -c Debug --no-restore -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` succeeded. The build reported zero warnings and errors.
- All 15 test projects passed complete, individual runs. The Server project passed 1,527 tests through the disposable Keycloak component verifier. The first concurrent run recorded timeouts in two parity-script cases, one governance-report case, and `FolderNavigationInteractionTests`. The complete serialized Contracts and UI E2E reruns passed all 775 and 64 cases respectively. The default runs skipped ten opt-in AppHost cases and two opt-in Forgejo cases.
- `python3 -m pytest -q scripts/tests` passed 76 tests. The whitespace check on `src`, `tests`, and `samples` reported no changes.
- The opt-in cross-process AppHost boot test passed. Both isolated Keycloak component verifier modes executed all six explicit AppHost cases with zero failures or skips.
- `pwsh -NoProfile -File tests/tools/run-forgejo-smart-http-alpine-smoke.ps1` passed on Forgejo 16.0.3 and 15.0.7. The public image pulls used a disposable Docker configuration after the desktop credential helper failed. The tested script, image tags, and expectations were unchanged.
- Regenerating the pending PD10 conformance manifest at each item commit kept all seven commits internally consistent. Squashing the review fixes preserved the tested source and configuration tree.

The ledger has 222 unclosed entries, comprising 75 numbered entries marked open and 147 legacy entries without a closure field. This includes the production tenant-access capacity follow-up.
