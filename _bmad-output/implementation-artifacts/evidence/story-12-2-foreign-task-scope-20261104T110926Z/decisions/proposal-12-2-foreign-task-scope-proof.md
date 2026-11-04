---
title: 'Story 12.2 foreign task scope and unsupported absence reconciliation'
status: 'approved-implemented'
created: '2026-11-03'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

## Owner approval — 2026-11-04

The user replied **approve** to the prepared two-probe correction. This authorizes only the exact
strict-body exception and focused source/authorization controls below. The patch was not applied
before approval. Prior sealed pending-decision snapshots remain immutable.

## Exact decision

The owner approved the [prepared test patch](proposal-12-2-foreign-task-scope-proof.patch) as an explicit
exception to the earlier prohibition on changing the original strict body. Change only its final
foreign-folder probe setup/input and add a distinct unsupported-absence probe. Keep the foreign task
scope **404 assertion** and require a real independently authorized different folder. The original
literal `absent-folder` will instead assert the already-approved **503 read_model_unavailable**
contract for unsupported physical source absence. This changes that physical-absence expectation;
it does not prove that a never-created stream can return404.

No production mapping, native proof protocol, actor/storage schema, source-reader privilege,
identity/grant, timing budget, retry, source-history replacement or other strict/capacity assertion
is included. No implementation was applied before this decision.

## Executed boundary

The latest unchanged strict run failed once, zero errors/skips, in362.951s at
`OperationalStatusDurabilityAcceptanceTests.RequireRealTaskClosureAsync:365`. It had passed provider
binding/rebuild, actual preparation and clean release, all six concern rebuild/retention checks,
interrupted generation, concurrent Workers, genuine tenant maintenance, process replacement,
post-restart terminal `ready`, equivalent replay, closed-binding denial and retained-prefix checks.
The next probe requested the real task with `folderId=absent-folder`; expected404, observed503
`read_model_unavailable` at the Folder ACL layer. Tenant evidence was Fresh. Source/head/runtime
drift was zero. All owned resources were removed; two unrelated Parties containers were preserved.
The test did not complete its remaining body, and SM4 was not qualified.

The actual native request acquired catalog authority in70ms and reached the receiver. Callback and
transport completed uncanceled; the caller received HTTP503 after380ms. This is not the earlier
catalog-acquisition starvation. The corrected SDK acquisition loop remains separately qualified by
222passing owner controls, including27new race cases, and the rebuilt/audited runtime.

## Why the existing input cannot establish the intended result

The [approved source-head proposal](proposal-12-2-current-sdk-source-head-transport.md) explicitly says:

> A never-created/unactivated aggregate without supported native evidence remains unavailable.

Only an existing valid zero-head proof can establish zero through that route. The current native
actor performs strong metadata and erasure-progress observations; missing metadata remains
Unsupported/Invalid with no proof. The required-proof controller consequently returns503. Both the
SDK reader and Folders adapter correctly refuse to infer zero. Existing control cases and the
latest live response agree with this accepted contract.

The strict probe is labeled **Foreign task folder scope**, but its literal input exercises physical
source absence before task storage can be considered. The corrected test will independently prove
both behaviors:

| Probe | Required actual evidence | Expected response |
| --- | --- | --- |
| Original task through a genuinely different existing folder | Real v1 creation; unchanged user permissions; current native source and normal Workers ACL/checkpoint; authorization for that different folder |404; original folder/task data must not be disclosed |
| Same task with never-created `absent-folder` | No supported native source proof; no bootstrap/seed/fallback |503 `read_model_unavailable`; no protected task lookup |
| Same task with missing folder hint | Existing missing-target refusal | Existing503, unchanged |

The task reader uses tenant+authorizedFolderId+taskId keys. After current layered authorization for
the different folder, the missing task slot in that folder can produce404 without observing the
original folder's protected task row. A create-only different folder has no task/workspace evidence.

## Bounded implementation after approval

1. Apply only the prepared patch to
   `tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs`: create a
   different folder through the existing authenticated v1 CreateFolder route with a fresh ordinary
   key/default task header, require202 and the existing current source/Workers evidence barrier,
   then use its genuine generated ID for the original404 foreign-task probe. Do not silently create
   an aggregate named `absent-folder`, inject a native zero anchor or write source/derived state.
2. Keep the untouched literal `absent-folder` as a separate503 probe with the canonical code. Retain
   the missing-hint503, original task terminal/replay/closed-binding/source-prefix checks and all
   remaining main-body assertions. Additional setup timings remain measured and identifiable; no
   sample or budget may be changed to manufacture an SM4 pass.
3. Add or extend focused real reader/handler/counting-store controls proving different-folder task
   keys only, no original-folder task observation, unknown native source remains unavailable and
   no task lookup before complete folder authority. Preserve the production stale/unavailable and
   valid-zero controls. If this needs a production behavior change, return the precise finding;
   this proposal does not authorize a new denial mapping or source-evidence capability.
4. Rebuild/audit the coherent Debug/project-reference runtime with both source flags, run affected
   controls and the revised strict class serially, retain all prior failed runs and record the exact
   owner-approved body hash transition. The other strict/capacity bodies and frozen spec block stay
   byte-identical. Preserve current SDK broad, Release/package, PD10/governance, SM4 and whole-story
   review gates independently. No dependency pin, publication, deployment or Git mutation.

## Reviewable bytes and remaining alternative

The strict file before approval was:
`f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90`.
The approved patch SHA256 is
`c832f58f6c1b05516c1b62f97e2bc8a820d6ed2b4a51f6006952db28a16d7856`;
its implemented strict file SHA256 is
`d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82`.
The implemented diff matches these exact approved bytes. The revised strict class passes1/1,
zero failures/errors/skips, in396.332s on the qualified current source producer. It executes both
the real different-folder404/no-disclosure probe and unchanged physical-absence503/code probe,
then completes the remaining restart/rebuild/replay/denial/storage-failure assertions. See the
[current qualification packet](evidence/story-12-2-foreign-task-scope-20261104T110926Z/README.md).
The four preceding failed attempts remain independent evidence. A fixture-only human-command
scope compatibility correction and opaque lifecycle diagnostics preserve all production policies,
source proof and deadlines; neither fabricates stop success or changes the approved strict bytes.

Current consumer selections pass5630cases with2existing optional skips; SDK Server broad passes7684
with25exact prior skips and three successful published ABI controls. The separate Release gate fails
CS1061 because packaged SDK4.0.4 lacks the required native metadata registration API. Actual strict
SM4 command/status p95 measurements remain over budget. This proposal's implementation and live
qualification do not close package/PD10, SM4, independent-review or whole-story acceptance gates.

Keeping the strict physical-absence404 expectation unchanged instead requires a separately approved
SDK authoritative-absence capability. No supported read-only seam presently establishes that fact;
existing seed paths write through command capture/maintenance. Such a capability would need its own
proof/design/qualification decision and cannot be replaced by converting missing proof, a404/503,
a missing projection or an erased/legacy stream into zero. No such capability is implemented here.
