---
title: 'Record the Folders resolution evidence source prerequisite'
type: 'chore'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
prerequisite_status: 'blocked-source-contract'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Establish the Folders-owned resolution evidence prerequisite using the Projects 2026-10-20 decisions and contract annex. Identify the actual provider and stable File identity API, distinguish Projects `FileReferenceId` from owner File identity, and reuse identity before adding any necessary owner mapping. Deliver an identity-only metadata authorization operation only when its source contract is executable. If that source is missing, record the precise missing contract without an unavailable stub.

Verify what the source establishes for Tenant, parent, lifecycle, permission and rename/delete/replacement. Prefer optional v1 native evidence after compatibility passes, otherwise an additive version. Add expiry only under an owner validity guarantee. Preserve existing work, history, dependency pins and commits.

</frozen-after-approval>

## Implementation Notes

- The owner checkout was clean on `main` at `efb98b1e5215ad73b4591f2a2a8ff00f8b8c794a`. The Projects workspace was at `16886b7d9c15b5356fad860b0d7b0f97eb9f32b3` with existing prerequisite-document edits. This is a bounded owner investigation for the Projects prerequisite, not Folders Story 6.4; no sprint status is changed.
- Two read-only source investigations established that repository adapters exist but a stable owner File identity/source contract does not. No unresolved product choice, irreversible action or executable runtime change remains in this request's missing-source branch. The one-shot deliverable is a new owner source-contract record and this workflow record; completing these records does not complete the runtime prerequisite.
- Deliverable: [source-contract record](6-4-resolution-evidence-source-contract-2026-10-20.md). Record actual APIs, namespace distinctions, native evidence losses, lifecycle limitations and the contract needed to resume. Leave runtime contracts, handlers, registrations, history and pins unchanged.
- Both focused Debug/source builds passed with zero warnings/errors. The existing dispatcher cases passed 6/6 and the four EffectivePermissions classes passed 15/15. A second focused execution retained [a local test receipt](evidence/6-4-folders-resolution-source-20261020/local-authorization-tests.json) with UTC times, source observations and stable compiled-input hashes. This small evidence artifact addresses review provenance; it does not qualify an immutable build-source tuple or the absent File operation.
- Preservation checks matched all 4,898 pre-existing Folders regular files and retained Folders/Projects HEADs and indexes. Other prerequisite work changed outside Folders concurrently and was left untouched. The selected runtime prerequisite remains blocked; `done` means this request's missing-source investigation/report branch is complete. User instructions override the workflow's commit step: nothing is staged or committed.

## Review Triage Log

The configured Blind Hunter review ran independently; no configured review layer was skipped. All five findings were checked against source and patched in the new report. None was deferred.

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Mapping collision scope and reference reuse were underspecified | Medium: ProjectAggregate checks its own FileReferences dictionary; FileReferenceUnlinked removes its entry. Tenant/reference alone could bind another Project or a later link. | Patch: record observed Project scope and the need to distinguish/invalidate link instances without prescribing an allocator. |
| Registration/lookup handoff was not explicit | Medium: ValidateLinkFileReferenceAsync receives no FileReferenceId and returns no OwnerFileId; the proposed owner query already needs OwnerFileId. | Patch: name the missing owner-verified registration and content-free lookup as distinct capabilities. |
| Producer composition defaults were omitted | Medium: domain DI registers unconfigured source/target resolvers; server DI registers unavailable workspace content storage. Low-level provider methods do not establish a configured authoritative producer. | Patch: cite the exact registrations and limit the claim to inspected default composition. |
| Resolution currentness bound was omitted | Medium: TenantAccessOptions defaults to five minutes, whereas annex section 6 requires the selected two-second resolution authority boundary. | Patch: explicitly require proof of that boundary; StrictRead success alone is insufficient. |
| Test-input provenance was not retained | Low: the report already denied production/immutable qualification, but sibling source builds can consume changing bytes. Original build-time source bytes cannot be recovered from current HEADs. | Patch: retain a timestamped rerun receipt with observed source states and stable compiled hashes; explicitly preserve the build-source provenance limitation. |
