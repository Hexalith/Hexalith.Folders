# Delivery boundaries and implementation work

The source-inspection task is complete. Its [report](../../implementation-artifacts/6-4-resolution-evidence-source-contract-2026-10-20.md) remains unchanged. This specification advances the missing capability through a concrete owner-source design; it is not another inspection receipt or an implementation/acceptance claim.

The selected Projects [D2/D6 decisions](../../../references/Hexalith.Projects/_bmad-output/implementation-artifacts/6-4-prerequisite-decisions-2026-10-20.md) and [Folders contract annex section 4](../../../references/Hexalith.Projects/_bmad-output/implementation-artifacts/6-4-resolution-evidence-contracts.md#4-folders-reuse-strict-authorization-add-opaque-file-evidence) remain in force. Their missing identity/registration capability is specified here. Their other owner, RTI, package and qualification work remains separate. Paths here use the actual root-declared Projects checkout; historical source-report links are not rewritten.

## Existing seams to extend

| Existing source, relative to Folders root | Reuse and limit |
| --- | --- |
| `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`; GitHub/Forgejo adapters | Reuse provider mutation/commit and existing canonical writer fencing. Add only the selected provider's real head/protection/outcome verification needed by the source. Current Add/Change/Remove operations do not establish edit/replace/rename identity. |
| `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`, `WorkspaceFileMutationAccepted.cs` | Connect authoritative write-side outcomes to explicit File intents. Existing accepted/staged mutation events cannot be relabeled as File lifecycle facts. |
| `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs` and effective-permission readers | Reuse exact `read_metadata`/StrictRead policy ordering. Retain independently scoped Tenant/Organization/Folder and File-policy revision evidence and add the final native comparison. Do not borrow merged watermark/decision timestamps as source verification. |
| `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs` | Reuse native Tenant facts; extend via a supported owner head/coverage seam. Age-based freshness does not establish this spec's currentness. |
| `references/Hexalith.Projects/src/Hexalith.Projects.Contracts/Events/FileReferenceLinked.cs`, `FileReferenceUnlinked.cs` | Preserve current reference namespace and event meanings; add optional owner-verification provenance to the new-link handoff and retain native envelope birth/sequence. No reference-generation allocator. |
| `references/Hexalith.Projects/src/Hexalith.Projects.Server/Projections/ProjectReads/ProjectReadProjectionHandler.cs` | Reuse supported atomic projection/rebuild patterns. Add a narrow link fact/head seam; do not read descriptive detail or rename an existing index into the missing source. |

No persistence implementation may use direct database/ORM/blob/raw Dapr storage. Use Hexalith.EventStore domain aggregates, projections and supported read-model/batch/recovery APIs under the [state baseline](../../../references/Hexalith.AI.Tools/hexalith-state-instructions.md). Missing technical plumbing belongs in EventStore/owner infrastructure, not a Folders workaround.

## Ordered work packages

These packages are implementation boundaries, not a dispatched sprint plan. No source handler, registration or enrollment is created by this document.

| Order / owner | Concrete change and exit condition |
| --- | --- |
| 1 — Folders domain/provider owners | Add same-named File command/event/state types under `src/Hexalith.Folders/Aggregates/File/`; implement explicit lifecycle and durable native tokens. Enroll one actual managed source profile with real provider enforcement/head checks and idempotent coordinated mutation recovery. Exit: AT-01–AT-11 demonstrate real persistence and the selected provider boundary. |
| 2 — Folders + native authority owners | Add content-free committed File/source read models and narrow source interfaces under `src/Hexalith.Folders/Files/`; preserve all native authority scopes in the permission readers. Obtain missing Tenant/caller/policy head support through its owning module. Exit: AT-12–AT-16 and AT-27–AT-29 prove two-pass currentness, safe denial, zero content and no read-side writes. |
| 3 — Folders + Projects | Add verification/registration/revocation domain facts and the full scoped binding projection under Folders. Extend Projects' additive link workflow, event provenance and narrow current link observation exactly as specified in contracts section 3. New Folders transport contracts live in `src/Hexalith.Folders.Contracts/`; per-type source operations live in `src/Hexalith.Folders.Server/Queries/` or the normal command pipeline. Exit: AT-17–AT-26 prove handoff, compatibility, crash/replay and race behavior. |
| 4 — Folders/Projects qualification owners | Run all applicable acceptance scenarios on the exact supported source/provider/SDK tuple; retain real state-store end-states and failure cases. Confirm source lookup works through enrolled verified transport and preserves existing APIs. Exit: AT-27–AT-30 and all preceding cases pass with no substituted stub or systematic Unavailable. |

New query operations reuse the generic SDK owner-query route. They require exact type allowlisting only in a separately authorized deployment step. No new module CLI/MCP server, host scaffolding, proprietary authentication, signing service or general ingestion framework is part of these packages.

## Dependencies and admission gates

- **Executable source:** choose and qualify the actual provider/source binding; the current unconfigured operation resolvers, unavailable content store and workspace metadata source cannot satisfy it. A source whose writer boundary cannot be enforced is ineligible for the first profile.
- **Native currentness:** deliver actual Tenant/caller revocation and policy/protection heads where current source lacks them. Unknown revisions are a blocked capability, not a string to invent. Keep the complete two-second boundary and all independent scope comparisons.
- **Projects handoff:** the current `ValidateLinkFileReferenceAsync` omits the reference ID and returns no owner binding; `RefreshFileReferenceAsync` is unavailable. The additive handoff must carry the full reference/link scope and owner verification pointer. Do not silently retrofit legacy path validation into this source.
- **Verified transport:** Projects D1 still owns production Tenant/domain/query-digest issuance and complete SDK-verified originating context. Existing development signing does not qualify the source. Reuse the supported gateway; no caller-supplied authority fallback.
- **Supported persistence:** use current accepted EventStore recovery/batch contracts. If cross-source non-current visibility or native head reads cannot be enforced, first deliver that narrow technical capability upstream. Do not update dependency pins or fabricate a successful package lane here.
- **Acceptance:** persisted lifecycle, provider controls, currentness, registration and lookup must pass before the source prerequisite can be accepted. This specification alone closes none of the parent, G-4, package, production enrollment or qualification gates.

Keep the existing `FolderOwnerReadResult` positional constructor, deconstruction and numeric Outcome representation intact. A separate native-evidence evolution may add optional v1 evidence only after old-reader/new-payload, new-reader/old-payload, absent/null evidence and non-current exclusion tests pass; otherwise it needs an additive version. No expiry guarantee is introduced. This source spec supplies facts for later evidence work and does not implement that change or any parent resolution handler.

Legacy references/files need separately authorized owner registration/re-verification/relinking. Do not create a backfill plan that assumes paths, blobs or old `ReferenceState` prove identity. Rollback/disable of future source adoption stops new-source use and retains events, terminal identities and existing routes; it does not erase history or make an earlier binding current.
