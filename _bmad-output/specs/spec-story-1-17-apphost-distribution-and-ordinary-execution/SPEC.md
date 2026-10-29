---
id: SPEC-story-1-17-apphost-distribution-and-ordinary-execution
title: Story 1.17 — supported AppHost distribution and ordinary source execution
created: 2026-10-29
status: draft-for-review
story_key: 1-17-publish-the-pd10-v2-authorization-contract-spine
tracker_policy: no-automatic-transition
companions:
  - apphost-distribution.md
  - qualification.md
  - ../../implementation-artifacts/story-1-17-supported-source-host-qualification-final-2026-10-28.md
  - ../../../docs/governance/approval-policy.md
sources: []
---

# Story 1.17 — supported AppHost distribution and ordinary source execution

## Why

Story 1.17 needs an ordinary command to execute the intended source-built Debug AppHost. The reviewed 202-test result establishes an earlier source build/model prerequisite. The command currently selects a prebuilt distribution independently of its later workspace mapping, and repository/catalog inputs have advanced. Neither compilation nor the historical loader probes establish which binary and dependencies the current ordinary command actually executes.

## Capabilities

- **CAP-1**
  - **intent:** A caller can select the intended prebuilt Builds Debug AppHost for ordinary source-mode execution.
  - **success:** The selection matrix in `apphost-distribution.md` passes; source mode cannot silently select a packaged host, and preflight and startup use the same recorded binary.
- **CAP-2**
  - **intent:** A reviewer can trace that binary and its demanded dependencies to the current mapped source tuple.
  - **success:** Fresh build/asset/compiler evidence establishes source origins, and observations from the real executing AppHost match the sealed paths, hashes, assembly identities and MVIDs described in `qualification.md`.
- **CAP-3**
  - **intent:** A reviewer can distinguish successful ordinary execution from build, model and refusal evidence.
  - **success:** A separately authorized bounded public command passes v2 preflight, reaches declared readiness and completes exact-run teardown, with matching process/provenance observations and no owned survivors or foreign resource changes.

## Constraints

- This invocation writes this spec folder only and stops for review. It authorizes no implementation, build/test execution or application/topology startup.
- Preserve existing work, including the six receipt-matching uncommitted Builds files. Perform no Git writes, dependency/reference changes, submodule initialization, Git-object repair, tool installation, publication or deployment.
- Keep the parent closure spec **in-progress**, tracker **backlog**, routing **V1Only**, **T0 unset**, activation authority **null** and staging **0/14**. Local runner qualification cannot change these values.
- Development uses Debug/source references; package/CI qualification uses its separate Release/package lane. Use production mapping and existing guards; private reference replacement, fabricated hashes, Hexalith package fallback and consumed loader approvals supply no ordinary-execution credit.
- Qualify current inputs afresh. Retain the four existing MSB3277 conflicts and the historical Parties object limitation without weakening checks or claiming complete historical coverage.
- Future offline SDK children require SDK 10.0.402, explicit bounded controls, preserved HOME, certificate prevention, private outputs/caches and supported serial execution. Production child controls require their own evidence before the future startup attempt.

## Non-goals

- Automatic AppHost compilation during `run`, a new CLI, a custom assembly loader, dependency/catalog modernization or repair of unrelated failures.
- Folders enrollment, full module/UI intake, signed package/release qualification, immutable hosted artifacts, deployment, staging, production identity/data acceptance, migration activation or parent closure.
- Replaying cold/host-first loader cases or extending their consumed approvals.

## Success signal

Phase A can establish **implementation/offline verified; ordinary execution pending**. Only Phase B's new, exact-tuple public-command receipt can establish **local source ordinary execution qualified**. A failed prerequisite, preflight refusal or startup failure retains its evidence and leaves that acceptance unmet; package and hosted gates remain separate.

## Assumptions

- The existing root-declared EventStore v2 declaration is the smallest real probe of the Builds runner and Platform model. Folders has no enrolled v2 owner declaration in this checkout; this probe grants no Folders intake acceptance.
- The existing `HEXALITH_G4_APPHOST` override is the supported ordinary path for a sealed private Debug output. Default source selection receives separate offline coverage.
