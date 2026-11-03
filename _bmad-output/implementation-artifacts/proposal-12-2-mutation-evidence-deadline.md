---
title: 'Story 12.2 mutation-evidence deadline prerequisite'
status: 'approved-in-progress'
created: '2026-11-03'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

# Owner decision

2026-11-03: The user selected **Approve 2-second mutation budget (Recommended)** in response to
the explicit deadline question. This authorizes the exact enabled mutation-evidence budget exception
and bounded implementation/qualification below. All other limits, proofs, authorization outcomes,
strict assertions and exclusions remain in force. The old 500 ms results remain retained evidence.

# Approved decision

Authorize one deadline exception: increase the total enabled mutation permission-evidence read budget
in `src/Hexalith.Folders/Authorization/PermissionProjectionCatchUp.cs` from 500 milliseconds to two
seconds. Its sole production opt-in is `WaitForCatchUp=true` for mutation evidence. Immediate reads
retain their current behavior. This is a change to the whole enabled read/retry budget, not a claim
that only projection polling is affected.

The exact production change proposed is:

```diff
-    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);
+    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
```

The approved [source-head proposal](proposal-12-2-current-sdk-source-head-transport.md) states:
“All listed exclusions, existing source/proof checks, human-command identity semantics and deadlines
remain in force.” The newly established 500 ms enclosing deadline is therefore preserved until this
exception was explicitly approved above. No production deadline change preceded that decision.

# Executed evidence

The final pooled-image runtime passed Debug/project-reference solution and Memories builds, with
92 selected modules matching their PDB/source hashes, one selected SDK variant and project-only
selected Hexalith assets. Source/head/runtime drift stayed zero throughout the unchanged strict run
on isolated port 36024. That run failed once, zero errors/skips, in 212.395 seconds, at the first
unknown-profile ConfigureProviderBinding PUT, whose original required outcome is 403.

The exact caller path is mutation evidence → EffectivePermissionsFolderPermissionEvidenceProvider →
EventStoreOrganizationEffectivePermissionsReadModel → PermissionProjectionCatchUp. Its 500 ms linked
budget includes the fresh independent source read. The source POST acquired its caller catalog in
204 ms across three CAS attempts, passed target validation and entered actual transport. The gateway
receiver then spent 222 ms acquiring its catalog, including 140 ms queued, and was canceled during
its second fresh capture. ReceiverNext, the workload action and source actor observation were not
reached. Cancellation occurred about 503 ms after the Folders handler began; the enclosing process
token remained uncanceled and returned a trusted 503 safe denial normally. SDK two-second, adapter
three-second, domain five-second and public ten-/thirty-second limits did not expire on this path.

Returned 503s continued across the fixture's existing retries until its 75-second total retry budget
canceled the final PUT. Its separate per-request HttpClient timeout remains 40 seconds. The final
result was TaskCanceledException, not an observed successful denial or a fixed source response.
Neither ResponseEnded nor the raw-actor refusal recurred. Owned processes, containers and the exact
fixture Dapr directory were removed; an unattributed separate container was preserved.

This run establishes a real 500 ms cancellation. It does not prove that further optimization could
never meet 500 ms, nor does it establish a completed source authentication/proof/result observation.
The retained compiled-inventory and image-buffer optimizations have independent focused and paired
component evidence; no additional measured optimization currently justifies another code change.

# Bounded implementation and qualification after approval

1. Change only the helper's enabled-read Budget to two seconds. Preserve its 25 ms poll interval,
   caller cancellation, monotonic total accounting, stale/unknown/unavailable distinctions and
   rejection of late or noncooperative available results. No permission, source or projection check
   may be removed, cached away or relaxed.
2. In `tests/Hexalith.Folders.Server.Tests/EventStoreOrganizationEffectivePermissionsReadModelTests.cs`
   and adjacent focused tests, qualify both organization and folder mutation scopes: valid authority
   after 500 ms but before two seconds; exhaustion without authority; stale evidence; noncooperative
   late completion; caller cancellation; and unchanged immediate reads. Adjust only the corresponding
   old unit deadline expectation. Original strict/capacity bodies and every denial assertion remain.
3. Rebuild and audit current runtime, run affected checks individually and rerun the unchanged strict
   real-provider class. The profile control still requires 403; later real-provider/task/durability
   checks and SM4 retain their own original evidence requirements. Preserve every failed attempt.

SDK two-second, source-reader three-second, domain five-second, HTTP forty-second and fixture
seventy-five-second budgets remain unchanged. No new transport, actor/persistence protocol, proof
bootstrap, raw-actor exception, identity/grant expansion, package/gitlink change, publication,
deployment, Git mutation or public v2 exposure is included.

The enabled mutation path can occupy resources and poll for up to four times as long. This may
increase contention; outer budgets still cap work and exhaustion still denies. Approval does not
waive SM4, guarantee live success or close the story's remaining owner/consumer, capacity, package,
PD10/governance or independent-review gates. Keeping 500 ms requires further separately measured
optimization and qualification; the current failing result cannot be accepted as 403.
