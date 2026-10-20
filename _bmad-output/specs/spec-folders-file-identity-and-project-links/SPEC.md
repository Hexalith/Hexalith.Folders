---
id: SPEC-folders-file-identity-and-project-links
date: '2026-10-20'
status: specified-not-implemented
companions:
  - contracts.md
  - acceptance-tests.md
  - delivery-plan.md
sources:
  - ../../implementation-artifacts/6-4-resolution-evidence-source-contract-2026-10-20.md
---

# Stable File identity, lifecycle and Projects link source

## Why

Projects cannot recover an authorized File target from its own reference ID. The completed Folders inspection found repository providers but no stable File entity or authoritative lifecycle source. This specification defines that missing owner capability and the registration handoff needed to use it. It does not resume parent Story 6.4.

## Capabilities

- **CAP-1**
  - **intent:** Folders can identify a File throughout its owner-established lifecycle independently of its location or bytes.
  - **success:** Proven edits, renames and moves retain identity; deletion, recreation and replacement cannot revive the retired File or its bindings (AT-01–AT-09).
- **CAP-2**
  - **intent:** Folders can establish current metadata authority for an exact File and parent without disclosing its address or content.
  - **success:** Native resource, source and permission observations pass a stable final comparison within the two-second request boundary; missing coverage never produces usable evidence (AT-10–AT-16).
- **CAP-3**
  - **intent:** Projects can register a reference to a File that Folders has independently verified.
  - **success:** Durable owner verification and the exact committed Projects link event establish one binding scoped by Tenant, Project, reference and link instance; conflicting or forged registration cannot overwrite it (AT-17–AT-21).
- **CAP-4**
  - **intent:** An authorized caller can recover the current binding without knowing a workspace or path.
  - **success:** Lookup validates current Projects membership and owner authority; unlink, relink, parent changes, replacement and stale deliveries cannot resurrect an earlier binding (AT-22–AT-30).

## Constraints

- Use Folders-issued ULIDs because the inspected executable provider surfaces supply no suitable File identity. A future reuse claim must name and test the actual identity API against this contract; repository IDs, paths, path digests, blob hashes and index-version IDs are ineligible.
- Keep canonical File identity, native File incarnation, native parent binding and Projects link instance distinct. Preserve existing Project/reference identifier bounds and comparison rules; introduce no reference-generation or permission-revision allocator.
- Persist owner facts through Hexalith.EventStore and supported projection/batch/recovery APIs. Queries perform no registration, ingestion, backfill, reconciliation or repair.
- Enroll only sources with enforced owner-controlled writes and verified current source coverage. Staging acceptance, Git rename inference, polling or an index tombstone cannot establish lifecycle authority.
- Exact File authority includes same Tenant, active File and parent, owner-resolved visibility/path policy, sensitivity classification and all native permission revisions. A Folder grant or five-minute Tenant freshness is insufficient.
- Reuse SDK-verified originating actor/workload/delegation and existing gateway transport. Payload fields, verification IDs and stored bindings grant no permission. Incomplete authority returns no usable evidence; cancellation propagates.
- The full field, lifecycle, currentness and failure contracts are in [contracts.md](contracts.md); all acceptance scenarios in [acceptance-tests.md](acceptance-tests.md) are required. Implementation boundaries and external dependencies are in [delivery-plan.md](delivery-plan.md).

## Non-goals

- Parent resolution handlers, scoring, RTI construction, legacy route changes or general ingestion infrastructure.
- An unavailable source stub, path-to-identity adapter, custom receipt service, authorization lease or unsupported validity window.
- Bulk adoption of existing files, legacy-reference backfill/relinking, history rewriting, package selection, deployment, enrollment execution or prerequisite acceptance.

## Success signal

A newly created File survives edit/rename/move with one identity. A verified Project link can recover it using only its scoped opaque identifiers; after unlink, parent change, deletion or replacement, the old link cannot return it. Persisted replay, two-instance races, provider enforcement and current-head tests demonstrate this behavior without content retrieval or read-side writes.

## Assumptions

- The first source profile covers explicitly enrolled canonical managed repository/ref trees. Workspaces stage mutations against those trees; arbitrary workspace snapshots are outside this identity namespace.
