---
title: 'Story 12.2 live-fixture tenant freshness prerequisite'
status: 'approved-in-progress'
created: '2026-11-03'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

## Owner decision

2026-11-03: The user replied **approve** to the fixture-only maintenance proposal after reviewing
the completed source-head/mutation-budget evidence. This authorizes only the exact disposable
administrator `tenants:update` permission, genuine authenticated metadata maintenance, and bounded
source/projection verification described below. All other limits and exclusions remain in force.
The earlier sealed packet retains the proposal's then-pending state as historical evidence.

## Approved decision

Authorize **only the disposable durability fixture's existing administrator** to receive the exact
`tenants:update` human permission and maintain its own test tenant through the existing authenticated
`UpdateTenant` command. Keep the production five-minute tenant-freshness policy, all other grants,
all deadlines, and the original strict/capacity test bodies unchanged. No implementation or grant
change preceded the approval recorded above.

The approved [mutation-budget proposal](proposal-12-2-mutation-evidence-deadline.md) explicitly
excludes "identity/grant expansion". The owner decision above is the sole additional fixture
permission exception. The approved two-second mutation budget is already implemented; this is a
separate issue.

## Executed evidence

The eighth current-source strict attempt failed once, with zero errors/skips, in 442.310 seconds.
It passed genuine administrator bootstrap, provider profile and target refusals, real preparation,
clean task release, exact six-concern rebuild, ordinary/rebuilt TTL assertions, interrupted-generation
recovery after changed process IDs, and concurrent writes from two Workers. Both EventStore restart
cycles reached `ApplicationStopped`; all original main-restart PID assertions passed.

The next authenticated task read at `RequireRealTaskClosureAsync:346` repeatedly returned 503 until
the unchanged HTTP retry budget ended. Payload-free diagnostics identify the actual denial:
initial bounded diagnostic tenant access was `Allowed/Stale`, then strict folder authorization denied
at `TenantAccessFreshness` with `tenant_projection_stale`. The protected task read model was not
observed. The first post-restart denial recorded age 309 seconds; 17 actual 503 replies continued through age
379 seconds. This is a correct application of
the unchanged five-minute policy, not evidence of missing durable task state or a shutdown hang.
Source/head/runtime drift was zero, and all owned processes, containers and the exact Dapr directory
were removed. Earlier shutdown failures remain separate retained failures with no speculative fix.

## Exact bounded change after approval

1. In `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityBootstrapConfiguration.cs`, add exactly
   `tenants:update` once to the existing disposable `admin-user` permission list. Do not edit the
   production realm, the ordinary Folders user, workload identities, source-reader scopes, or any
   other administrator grants. Keep actual issuer/catalog/channel/resource authorization.
2. In `FolderDurabilityAcceptanceFixture.cs` and a small fixture helper if needed, perform bounded,
   foreground tenant maintenance before long restart/protected-observation phases when its last
   verified source evidence approaches expiry. Use the existing canonical platform-scoped
   `SubmitAsync` path and existing administrator credential:

   ```csharp
   await SubmitAsync(TenantPlatformScopes.Tenants, "tenants", TenantId, "UpdateTenant",
       new { tenantId = TenantId, name = "Durability acceptance",
           description = "Disposable local acceptance tenant" }, _adminToken);
   ```

   Current Tenants source emits a real `TenantUpdated` event for this authorized command. The name
   and description remain the fixture's original values. Membership, roles, enabled state, tenant
   identity and Folders authority must remain equal. Wait for terminal `Completed`, observe the
   actual emitted tenant source event, and verify normal Folders projection delivery reaches its
   source sequence/timestamp with unchanged authority semantics. Acceptance alone is insufficient.
3. Use existing foreground fixture seams such as `WaitForCurrentFolderEvidenceAsync` and
   `RestartHostsAsync`; avoid a new background heartbeat. Define a bounded pre-expiry threshold
   below five minutes. Missing, malformed, conflicting, rejected, timed-out or undelivered evidence
   must fail the fixture. Never stamp timestamps, write tenant source/derived state directly, replay
   an old event as fresh, infer source zero, or change authorization to accept stale evidence.
4. Add focused real-realm/model and fixture-policy controls: exact permission addition; unchanged
   other identities/grants; no maintenance while sufficiently fresh; exact own-tenant/canonical
   command shape; failed command or lagging projection cannot establish freshness; unchanged
   membership/enabled state; bounded cancellation. Retain production stale-denial controls.
5. Rebuild/audit the current source runtime and rerun the unchanged strict class with exact resource
   cleanup. Preserve all prior failures and source/runtime evidence. Keep folder retained-byte and
   prefix assertions unchanged; the extra real events belong only to the fixture's Tenants stream.

The exact new permission is necessary: `UpdateTenant` has a catalog declaration requiring
`tenants:update`, with no administrator bypass. The current fixture administrator does not have it.
Repeating `AddUserToTenant` for the existing member is rejected, so it cannot stand in for a genuine
metadata update. Adding/removing members or inventing rejection freshness is excluded.

## Limits and alternatives

This fixture would qualify a tenant maintained through real authorized source activity. It would
not qualify indefinite access for an idle tenant; existing stale-denial tests and production policy
remain authoritative. The additional command has real cost and may itself fail. No live success,
performance pass or whole-story completion follows from approval.

Keeping all grants unchanged leaves further measured optimization on the already-approved native
authority/bootstrap paths as an alternative. The current evidence does not establish a sufficient
remaining optimization or justify changing a timeout or accepting the failed task read.

SDK retained-catalog parity, red-phase/optional SDK lanes, Release/package NU1109, current-cohort live
capacity, PD10/governance, SM4 and independent whole-story review remain separate gates. No package
or gitlink update, publication, deployment, staging, commit, push or public v2 exposure is included.


## Approved component qualification — 2026-11-03

The exact grant/foreground maintenance implementation is now qualified by the separate real natural-
age control (1pass, zero failures/errors/skips,183.007s),101focused fixture cases and repeated genuine
maintenance inside unchanged strict4. Original retained Tenant source prefixes and normal persisted
Workers authority/evidence/no-op controls pass. Production freshness5min and every approved deadline
remain unchanged. All owned resources were removed. See the [continuation packet](evidence/story-12-2-tenant-maintenance-20261103T211501Z/README.md).

Full strict acceptance remains open at an independent later task-scope probe: native metadata for a
never-created folder is unsupported and returns503, while the old probe expects404. Tenant was Fresh;
restart task state/replay passed. The [prepared scope-contract exception](proposal-12-2-foreign-task-scope-proof.md)
is not applied and requires a new owner decision. This does not alter this proposal's approved scope
or establish whole-story/package/SM4 acceptance.
