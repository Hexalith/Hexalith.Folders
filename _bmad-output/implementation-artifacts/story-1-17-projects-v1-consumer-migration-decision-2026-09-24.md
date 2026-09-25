# Story 1.17: Projects v1 consumer migration decision

Status: option comparison. Jerome accepted the bounded coexistence plan in the 2026-09-24 conversation, as recorded in the [current approval package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md). No exposure, release, or technical-gate completion is recorded here.

## Verified production evidence

- [Folders production deployment 6546836679](https://api.github.com/repos/Hexalith/Hexalith.Folders/deployments/6546836679) selected `823dce16bca7d1eb9c10c4d871bf581fc9dc61ef` and has a [successful status](https://api.github.com/repos/Hexalith/Hexalith.Folders/deployments/6546836679/statuses).
- [Projects production deployment 6547910236](https://api.github.com/repos/Hexalith/Hexalith.Projects/deployments/6547910236) selected `c767d38d8ae76f9ad949965ac4870a842c699f9c` and has a [successful status](https://api.github.com/repos/Hexalith/Hexalith.Projects/deployments/6547910236/statuses).
- The deployed Projects commit [pins `Hexalith.Folders.Client` 1.0.0](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/Directory.Packages.props) and [uses its generated client for folder lifecycle and effective-permissions reads](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/src/Hexalith.Projects.Server/Folders/FoldersProjectFolderDirectory.cs). The [NuGet version index](https://api.nuget.org/v3-flatcontainer/hexalith.folders.client/index.json) lists `1.0.0`.
- The refreshed local [consumer discovery](../../docs/contract/pd10-v2-consumer-discovery.md) supersedes its 2026-09-19 no-deployment claim. A deployed external v1 consumer exists under Story 1.17's stop rule.

## Candidate held for decision

The isolated branch `feat/story-1-17-v2-closure-prep` contains the corrected, unrouted v2 candidate. Its 206-artifact manifest is `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`, SHA-256 `e89ee58008e3ff4b34d08eac6e8d6a3d357d7df3ce7b1f13eadd53781e8e500c`, with candidate-set SHA-256 `d2c30653d9292c4d61122620cff5a61ebc1f7de1e2c11a6daf64c11d540a33cd`. The old A6b approval binds different bytes and remains historical; the current candidate is checked against its own technical evidence. Production `Program.cs` still selects v1. Main and sprint-status are unchanged.

## Practical choices

### A. Coordinated switch

**How it works:** Build and test Projects against the exact v2 generated client in its own repository first. In one scheduled window, deploy the verified Folders v2 route and the Projects v2 client, verify its lifecycle and effective-permissions calls, and retire v1 only after those checks pass. Keep both prior deployment artifacts ready until the observation period ends.

**Pros:** Shortest period serving v1; one coordinated release event; no temporary two-route profile after the switch.

**Cons:** Folders and Projects deployments must line up closely. A failed or delayed Projects deployment can leave its v1 client unable to call Folders, so reversal has to restore both services together. This is a poor choice unless a preproduction rehearsal proves the order and rollback.

### B. Bounded coexistence — owner accepted, entry checks pending

**How it works:** Within the owner-accepted, consumer-specific window, expose the verified Folders v2 route while keeping the existing v1 route working for Projects. Deploy Projects with the exact v2 client from its owning repository, verify real v2 calls, then retire v1 after the exit checks pass. Routes are versioned independently; no dual-write, translation proxy, or relaxation of v1 security rules is proposed.

**Pros:** Projects can be switched and reversed independently while its prior v1 route remains available. A real observation period can include ordinary scheduled calls before v1 is retired.

**Cons:** Temporarily serving v1 is an explicit exception to the target production profile. It adds one route to monitor and requires reliable evidence of which version Projects calls. If that evidence is unavailable, the exit condition cannot be proved.

### C. Continue the hold

**How it works:** Keep `Program.cs` and production routing as they are. Finish Projects v2 preparation and technical checks, then start B only when the deployment owner can commit to the accepted window.

**Pros:** No new production exposure or coupled deployment risk today.

**Cons:** Story 1.17 and v2 adoption remain incomplete; the deployed v1 dependency persists.

## Recommended window and checks for choice B

**Accepted duration:** Seven calendar days. `T0` is the earlier of production v2 route enablement or its first production request, recorded in UTC; the deadline is `T0 + 168 hours`. Deploy Projects v2 within 24 hours of T0, then observe at least 24 hours of successful ordinary traffic and exercise each inventoried periodic Projects Folders call at least once before v1 retirement. Delivery records the UTC activation slot and calculated deadline before activation. The [approval package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md) owns the detailed runbook and evidence checks.

**Entry checks:** Validate the current A6b candidate, run current-input Section 9, confirm the A8 condition, and keep v2 exposure off until all entry evidence passes. Projects' owning repository builds and tests against the exact published v2 client before `T0`, covering lifecycle, effective permissions, and file metadata. Demonstrate version-attributed request evidence, a working v1 response, and a rehearsed reversal using the prior Projects deployment and Folders route configuration. Discover any other deployed v1 consumers before retirement.

**Exit checks:** Record the Projects v2 deployment commit and successful deployment status. Verify lifecycle, effective-permissions, and file-metadata calls through v2 with metadata-only evidence; no unexpected canonical 401/404/503, response-deserialization failure, or elevated error rate against the pre-switch baseline. Show no Projects v1 traffic after successful v2 smoke during the observation interval, and confirm no other deployed v1 consumer remains. Retire v1 only after those checks pass, by the deadline.

**Stop and rollback:** Halt on an authorization or disclosure regression, failed Projects smoke call, unexplained v2 errors, inability to attribute route traffic, or failure to meet the deadline. Restore the prior Projects v1 deployment, keep or restore its working Folders v1 route, disable v2 exposure, and verify lifecycle, effective-permissions, and file-metadata calls. Record the deployment identifiers and metadata-only outcome. If the seven-day deadline expires without exit evidence, end the exception and seek a new decision; do not silently extend it. This plan does not claim to reverse v2 mutations, which need separate evidence before any mutating Projects use.

The staged checks follow the general practice of monitoring health between rollout phases and initiating recovery when a phase fails, as described in [Microsoft's safe deployment guidance](https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/safe-deployments). The seven-day cap and Projects-specific checks are proposed here from the repository's PD10 constraints, not prescribed by that guidance.

## Recommendation and decision status

Jerome accepted **B** under the [project decision policy](../../docs/governance/approval-policy.md). **C** remains the executable state until B's entry checks pass. A material change to the accepted scope or seven-day cap needs a new owner decision. The [approval package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md) records the remaining checks. No `Program.cs` change or deployment has occurred.
