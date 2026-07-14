# Input Reconciliation — Architecture

## Input

- Source: `architecture.md`
- Compared with: `prd.md` (updated 2026-07-14)
- Addendum: none present
- Exclusions requested for this pass: implementation mechanics and the already-explicit C7, canonical file-policy, and authorization-matrix open items.

## Overall Alignment

The architecture reflects most of the updated PRD: the OpenAPI Contract Spine and generated SDK authority, REST/CLI/MCP/SDK parity, repository-backed-first scope, provider-confirmed durable commit semantics, authorization freshness, metadata-only audit, no incoming webhooks, metadata-token-only FR58 materialization, projection-first console behavior, safe incident-mode evidence, current C3/C4 values, and the 11-state workspace vocabulary.

Five product-level issues remain. The first two are direct current-release capability gaps; the last three are stale or internally inconsistent architectural contract statements that could implement weaker behavior than the PRD now requires.

## Product-Level Gaps and Contradictions

### 1. FR58 is a current-release capability, but the deployed architecture intentionally returns no usable results

The PRD lists authorized metadata-token recall and indexing-status queries as MVP must-haves and FR58 requires them through REST, SDK, CLI, and MCP. Architecture correctly narrows materialization to metadata-derived tokens, but its `Query Facade (Story 10.5)` section records that the deployed Server uses `UnavailableSemanticIndexingBridgeReadModel`: search returns `Allowed` with zero items and indexing status returns `ReadModelUnavailable` until Story 11.10.

Fail-safe behavior is correct during an outage; making that behavior the deployed default is not delivery of FR58. The architecture's statement that this is a "documented deferral, not a defect" conflicts with the PRD's current-release scope. Story 11.10 (or equivalent production wiring and evidence) must be a release prerequisite, or FR58 must be explicitly removed from MVP through product scope change.

### 2. The MVP operations console cannot deliver its required trust and incident outcomes with empty production read models

The PRD requires the MVP console and status surfaces to expose readiness, lock, dirty state, failure, provider/sync status, projection freshness, transition evidence, and metadata-only incident reconstruction. Architecture's `Ops Console & Transition-Evidence Read Models (MVP limitation)` says the deployed diagnostics and transition-evidence read models are test-only seed seams, empty in production, have no projection logic, and return `NotFoundSafe` for every production query until Story 11.10.

Safe-empty is preferable to fabricated data, but it does not meet User Success, SM6, FR31, FR46, or FR52–FR56. The architecture's claim that empty-in/empty-out still counts as covered determinism conflates technical determinism with product capability. Production-backed diagnostic and transition-evidence projections plus release evidence must be required before MVP acceptance, or the console scope must be formally reduced in the PRD.

### 3. The lock serialization identity is stale and weaker than the PRD

The updated PRD defines the single-writer identity as managed tenant plus canonical provider/repository identity plus normalized target ref, and requires every folder binding, alias, workspace, and task resolving to that identity to collide. Architecture cross-cutting concern #4 still describes one active writer per `tenant/folder/workspace` scope, while its Locking process section never replaces that scope with the canonical repository/ref identity.

The stale scope could allow two logical folders or aliases bound to the same remote/ref to hold independent locks and produce mixed commits. Update the architecture invariant, durable lock key, transition tests, and parity evidence to use the PRD identity; folder/workspace/task IDs may remain lock metadata but cannot define collision identity.

### 4. Idempotency scope is inconsistent and several architecture decisions retain the old subset

The PRD now requires idempotency for every mutating Contract Spine operation, explicitly including folder creation, provider/repository binding, branch/ref policy, ACL updates, archive, workspace preparation, lock acquire/release, file mutations, and commit. Architecture's Requirements Overview, cross-cutting concern #3, D-7 examples, and A-9 still scope idempotency to prepare, lock, file mutation, commit, and cleanup. Only the later Process Patterns section says every mutating command must carry a key.

That internal inconsistency can leave administration and lifecycle mutations outside generated hash rules, TTL declarations, parity rows, and no-duplicate evidence. Make "every mutating Contract Spine operation" normative in A-9/D-7 and all summaries, and require the generator/gates to prove complete coverage rather than retaining the earlier lifecycle subset.

### 5. Incident-mode authority and cross-tenant operator wording do not match the PRD's scoped-access rule

The PRD allows incident event evidence only when the actor has both incident-admin permission and normal tenant/folder access; MVP has no global cross-tenant browsing or unscoped break-glass authority. Architecture F-6 describes the incident stream as available to operators with `eventstore:permission=admin` and does not explicitly require the second, normal tenant/folder authorization. S-6 also refers to "cross-tenant operator views," which is outside the MVP access model even if values are redacted.

Make the conjunctive authority explicit: incident-admin **and** current tenant/folder access, with bounded correlation/time-window evidence and safe denial. Remove or clearly mark cross-tenant operator views as post-MVP so redaction cannot be mistaken for authorization.

## Material Correctly Kept Downstream

The following architecture content does not need promotion into the PRD: package and project layout, aggregate class names, Dapr app IDs and topics, state-store/pub-sub selection, snapshot intervals, process-manager implementation, NSwag/Octokit/Forgejo client choices, working-copy paths, deployment topology, health-check implementation, code organization, event/command type names, and specific CI tool choices. Those decisions are implementation mechanisms as long as they satisfy the PRD's security, parity, visibility, durability, and release-evidence outcomes.

The architecture's metadata-derived Memories materializer is aligned with the updated PRD; its older authorized real-content indexing path is explicitly deferred and therefore is not a current contradiction. Likewise, the no-webhook MVP posture is aligned.

## Reconciliation Result

The PRD and architecture are not release-aligned until the deployed FR58 facade and operations-console read models are production-capable. In parallel, the architecture must adopt the repository/ref lock identity, universal mutation idempotency scope, and conjunctive incident-view authorization now stated by the PRD.
