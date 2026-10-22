# Story 12.3 content prerequisite proposal

Status: approved for implementation by Jerome on 2026-10-22. Created 2026-10-22.

Parent: [Story 12.3 draft](spec-12-3-durable-workspace-file-content-store-and-content-read-source.md).

## Observed gap

The current EventStore SDK has no supported authoritative file-body API. `IReadModelStore` explicitly owns derived read models; using its generic serializer to store bodies would create a new domain persistence protocol and put content in projections. Payload protection is not an authoritative content service, and reversible protection cannot satisfy PD8 for confidential operational values.

Folders currently discards decoded inline content and accepts streamed descriptors without reading their referenced bytes. `IWorkspaceFileContentStore` receives only metadata. The production content, removal, path-evidence and context implementations are unavailable placeholders. OQ2's approved artifact still matches SHA-256 `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`.

Story 12.1 is complete; 12.2 is not a direct prerequisite. Story 12.7 is backlog: this proposal does not claim that its tokenizer or provider-capability implementation exists.

## Proposed scope

Authorize a generic capability in the root-declared `references/Hexalith.EventStore` repository, followed by Folders consumption. EventStore owns the physical persistence mechanism and authenticated upload/read/disposition transport. Proposed new API files belong under `src/Hexalith.EventStore.DomainService/Content/`; finalize names and backend integration after inspecting that repository's guidance. These are proposed additions, not existing SDK APIs.

The capability must:

- Accept actual bounded bytes/streams, calculate and verify server-side hashes and lengths, and issue immutable tenant-scoped opaque receipts. Existing Folders stream descriptors reference these verified uploads; caller assertions never establish stored content. Any required transport contract/SDK changes are documented and generated, without enabling public v2.
- Keep bodies separate from metadata events, actor snapshots, projections and telemetry. Bind publication to authoritative command, tenant, folder/workspace/task, file incarnation, current lock fence and expected version. Upload possession alone grants no read or mutation authority.
- Make uncommitted uploads invisible. Recover publication interrupted before/after event append without an accepted file pointing to absent bytes, a rejected batch becoming visible, duplicate logical effects or tombstone resurrection. EventStore remains the sole writer of domain metadata and owns the generic recovery protocol.
- Read exact staged/current committed versions and enforce integrity, bounded cancellation, missing/corrupt-state failure, deletion suppression, current hold rules and tenant isolation across restart/replicas. Working copies remain disposable; Memories is not a body authority.
- Preserve PD8: no confidential operational cleartext, reversible sealed value or reconstruction material enters any durable body namespace or working copy. Use supported tokens/irreversible handles; reject unsupported execution before admission. This prerequisite does not implement the full Story 12.7 tokenizer or change its governing policy.

No deployment, publication, dependency upgrade, nested submodule work or version-control recording is proposed. Source qualification precedes any separately necessary package rollout.

## Proposed authoritative-body retention decision

Create an explicit C3 class for authoritative content, rather than inheriting a cache TTL:

- Keep every currently referenced staged/committed version while its folder/file remains live, with no automatic body TTL. Do not expire unresolved staged work due to a recovery deadline, lock loss or projected-status expiry.
- Accepted removal or the approved tenant-deletion workflow immediately denies ordinary retrieval. Hold may preserve physical bytes but never restores ordinary visibility. Replay preserves the disposition and does not recreate deleted versions.
- Superseded, removed and abandoned upload bytes become eligible for physical deletion only after no live authoritative reference remains and associated work is terminal with no active task. Start an explicit seven-calendar-day cleanup epoch then; fresh eligibility and legal-hold checks are required at deletion. Legitimate resume cancels the epoch; later eligibility starts a new one. Pending/indeterminate publication remains protected until reconciled. Uploads that never became a task use a durable terminal abandoned-upload disposition before this clock starts.
- An active hold blocks physical deletion; release requires a fresh qualifying epoch. Retain only independently governed metadata receipts/tombstones after body erasure. Archiving alone does not authorize body deletion. Physical deletion is irreversible; it does not rewrite retained events or shorten audit retention.

This is a proposed extension of C3. The existing P7D approval covers disposable working files and does not approve authoritative-body deletion.

## Risks and required checks

The main risks are cross-tenant receipt misuse, admission/publication crash windows, fencing races, leaked bytes, unbounded orphan accumulation and irreversible cleanup. Before dependent implementation is accepted, prove authenticated tenant isolation, hash/length verification, atomic visibility, conflicting/equivalent replay, replaced-process recovery, multi-replica fences, corrupt/missing bodies, timeout/cancellation, deletion/replay suppression, legal hold, source-bound evidence and a real persisted-byte round trip. Inspect physical stored contents and ensure events/audit/telemetry contain no bodies. Run focused EventStore checks first, then Folders gateway/process integration and opted-in Aspire acceptance with zero skips.

## Decisions requested

1. Authorize this EventStore prerequisite plus Folders consumption, or keep implementation Folders-only and wait for a supported platform capability.
2. Approve the proposed authoritative-body retention rule, or provide a different retention/deletion schedule.

One response from Jerome can decide both under [the project decision policy](../../docs/governance/approval-policy.md). Jerome approved both decisions with "approve both and continue" on 2026-10-22. Required technical checks remain outstanding; no runtime completion is claimed.

## Owner decision record

Decision maker: Jerome. Response: "approve both and continue". Proposal reference: this document and its parent spec. Accepted scope: the complete EventStore prerequisite and Folders integration described above, including the proposed authoritative-body retention/deletion rule. Outstanding checks: every item in Risks and required checks. This decision authorizes implementation and qualification; it does not authorize deployment, publication, dependency upgrades or version-control recording.
