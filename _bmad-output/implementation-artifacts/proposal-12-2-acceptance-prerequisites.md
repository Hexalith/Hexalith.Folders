---
title: Story 12.2 acceptance prerequisites
status: approved-in-progress
created: 2026-10-18
parent_spec: spec-12-2-durable-projections-and-task-completion-pipeline.md
context:
  - _bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md
  - _bmad-output/implementation-artifacts/epic-12-context.md
  - docs/exit-criteria/c3-retention.md
  - docs/contract/authorization-matrix.md
---

# Decision requested

Authorize a separate prerequisite change for the three gaps below, then resume the unchanged Story 12.2 acceptance matrix. This proposal is not authorization to deploy, expose public v2 routes, commit, push, replace retained events, or implement provider commit execution/content storage/idempotency/cleanup.

## Reviewable result at approval

The working tree implements deterministic operational folds, SDK batch persistence and retained-manifest recovery, metadata readers, source-folded task closure, worker delivery before acknowledgement, and protected full-prefix repair through the existing `/project` route. Component tests cover committed/clean-release/failure/unknown results, conditional races, interruption recovery, retention expiry and task folder isolation. The real-source live scenario covers folder creation, stored contents, changed-PID restart, repeated empty-checkpoint rebuild, distinct Server/Workers consumers and safe version/corruption/foreign-tenant/storage failures.

Full Story 12.2 is not accepted. Provider-backed clean release, a current folder-authorized public task read, named SDK rebuild transport, actual scaled Workers convergence and live interrupted-batch recovery remain open. Server and Workers hosting the same writer are two processes, but do not establish a scaled Workers deployment. Synthetic lifecycle histories and the platform protocol harness are component evidence only.

## Bounded prerequisite changes

1. **Provider binding and readiness.** Replace the unconfigured production repository-target resolution and process-local readiness-binding source with the applicable owner-approved durable provider binding/readiness path. Use existing SDK/domain seams and real provider credentials resolved privately. Prove a real authenticated folder can bind, prepare, lock and cleanly release a task against a disposable supported remote; preserve the resulting event stream. Provider commit execution, file bodies, confidentiality admission and destructive cleanup stay with their existing owners. Do not inject a ready binding or successful provider outcome to satisfy acceptance. Start by reconciling the Story 3.14/3.5 owners rather than moving their scope implicitly into 12.2.

2. **Folder-authorized task lookup.** Approve an additive v1 target hint: an optional `folderId` query parameter on the existing `/api/v1/tasks/{taskId}/status` route. Validate the hint, preserve the existing `read_task_status` claim check and establish current layered folder read authority under the approved status/inspection policy, then pass the authorized folder and watermark to the existing durable reader. Missing scope continues to fail closed. The already implemented task key includes tenant/folder/task, so a foreign-folder task returns safe missing without reading its row. Update the v1 contract and affected adapters/tests together; preserve the response shape and keep public v2 disabled. If this contract choice is declined, an owner must supply another pre-authorized folder-scoped caller path before the task acceptance row can close.

3. **SDK-owned named projection transport.** The pinned EventStore SDK publicly maps the whole domain-service endpoint set; its selective named rebuild mapper and replay-header binding are private/internal. Qualify a public SDK seam that maps only the named projection/rebuild routes with the platform's existing workload/replay guards, then consume it without duplicating Folders process/query/replay-state routes. Any SDK/package/gitlink change requires explicit dependency scope authorization and revision-bound verification. Do not copy or weaken the platform replay protocol locally. The existing protected `/project` full-prefix repair remains the currently verified transport.

## Acceptance after the prerequisites

Run the existing domain, Server and Workers projects individually, build the Debug source graph, and run opted-in authenticated acceptance with zero failures/skips. Extend the task-owned fixture to prove all six concerns from real retained events, current folder/task denial, clean-release terminal evidence after changed-PID restart, equivalent replay versus later new-write rejection, two concurrent Workers replica processes, live interrupted-batch recovery, empty-checkpoint rebuild, retention boundaries and unchanged source hashes. Retained commit-success events may establish commit-status folding; real commit execution remains Story 12.4. Keep each unavailable or unexecuted row open.

## Approval effect

An approval of this proposal authorizes planning and implementing these bounded prerequisite changes, including the additive v1 folder hint and qualification/consumption of the minimal EventStore capability. It does not authorize a remote operation, dependency publication, deployment, public v2 exposure, or a claim that Story 12.2 is complete. Record the owner decision here and in the parent spec before editing any previously frozen boundary.

## Owner decision

2026-10-18: Jerome selected **Approve the bounded prerequisites** in response to the explicit scope question linking this proposal. The three bounded changes above are authorized. Public v2 remains disabled; no deployment, publication, commit or push was authorized.

## SDK qualification finding

Qualification of the approved named/shared rebuild capability found an existing internal owner-checkpoint defect: a successful prefix rebuild followed by ordinary stream growth retains the earlier captured identity vector, then wrongly rejects the next complete-source rebuild because that shorter vector cannot equal the full history. A task-owned real-Dapr reproduction preserves the original DTOs and proves prefix 1 -> ordinary 2..8 -> full 8 reaches this exact failure. The minimum correction verifies all previously captured identities against their matching prefix and extends an identical shorter prefix through the existing CAS path; conflicting, malformed or truncated vectors remain rejected. This corrects the capability already being qualified without adding a public API, protocol, storage schema, product limit or release operation. The original owner decision is unchanged.

## Current local qualification

Historical qualification is stage five in [the evidence record](evidence/story-12-2-durability.json): 3,908 component passes, two existing optional Domain skips, and a real-provider acceptance pass with zero failures/skips in 311.385 seconds. Those results bind the exact recorded source. Stage six records 4,193 final-source component passes and a strict real-provider acceptance pass with zero failures/skips in 299.439 seconds. Full Story 12.2 acceptance remains open for the proposed generic EventStore bounded owner-identity capability, delayed source-deadline expiry, interrupted retained scratch-batch recovery and the final over-budget SM4 acknowledgement observation; no additional SDK scope is approved.

The selective mapper and internal prefix-extension correction are in the local unpushed EventStore commit `d4d05f871991ec2af305267f59dcefeae3f127ed`, while the Folders gitlink remains `84bb115a0ced2497ca4315644a677b273cb44edf`. Separate Release/package qualification and coordinated consumption remain required; the EventStore 3.125.1 catalog does not establish qualification of that commit. Public v2 remains disabled. The owner decision and excluded work above are unchanged.
