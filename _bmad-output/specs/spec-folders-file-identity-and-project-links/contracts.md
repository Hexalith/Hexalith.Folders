# File source and Projects binding contracts

These are required new contracts, not claims about existing APIs. `MUST` is normative. Types listed below belong in separate same-named files when implemented. No handler or source implementation is delivered by this specification.

## 1. Identity and scope

| Field | Contract |
| --- | --- |
| `TenantId` | Exact managed Tenant established by the SDK-verified caller. Never accepted as authority from a payload. Every owner key, source observation and binding includes it. |
| `OwnerFileId` | New Folders-issued canonical 26-character ULID, validated with `Ulid.TryParse` and canonical round-trip equality. Assigned once by an authorized create/register command and persisted before use. Unique within Tenant; never reissued. No trim, case folding or derivation from location/content. |
| `FileIncarnationId` | Native `MessageId` of the committed `FileCreated` event. Immutable for that File; replay retains it. It supplies lifecycle generation evidence without a new generation allocator. |
| `ParentFolderId` | Exact current owning Folder in the same Tenant. Membership is an owner fact, independent of caller-supplied expected parent or Folder permission. |
| `ParentBindingId` | Native `MessageId` of `FileCreated`, subsequently of the latest committed `FileMoved` event changing the owning Folder. A move back creates another token. Same-Folder path renames preserve it. |
| `FileRevision` | Native File stream sequence, scoped to `(TenantId, OwnerFileId)`. It changes on lifecycle/resource events; it is not identity. |
| `ProjectId`, `FileReferenceId` | Existing Projects identifiers with their current validators, ordinal identity comparison and domain bounds, including the persisted 128-character reference limit. They are not File IDs. |
| `LinkInstanceId`, `LinkSequence` | Native committed `FileReferenceLinked` envelope `MessageId` and `SequenceNumber`, scoped to `(TenantId, ProjectId)`. No-op command replay creates no new instance. Unlink followed by a new link has a new instance even when reference text is reused. |
| `OwnerVerificationId` | Native committed owner-verification event `MessageId`. An opaque pointer to owner facts, never a bearer grant or an identity alias. |

The binding key is exactly `(TenantId, ProjectId, FileReferenceId, LinkInstanceId)`. Its immutable target is `(OwnerFileId, FileIncarnationId, ParentFolderId, ParentBindingId)`. Retain `LinkSequence` and the verification pointer as provenance. Unambiguous tuple encoding is mandatory; delimiter concatenation without escaping/length framing is insufficient. A current-slot index may point from `(TenantId, ProjectId, FileReferenceId)` to an instance, but cannot authorize lookup or replace the full key.

Native event IDs refer to persisted `EventEnvelope.Metadata.MessageId`; sequence refers to `Metadata.SequenceNumber`. `ProjectionEventDto.MessageId` is optional in the existing SDK, so new lifecycle/link projections must require the actual persisted value when building these tokens. Missing provenance is non-current; do not synthesize IDs from sequences, timestamps, paths or index versions.

One File represents an owner-managed entity in one canonical source tree at a time. Workspace IDs, repository/ref addresses, normalized paths, Git object IDs and content remain internal location/mutation data. Copies, branch copies and identical bytes at multiple locations have distinct File IDs unless an explicit owner move transfers the existing entity. Identity lookup never searches by bytes or guesses a rename.

**Reuse rule:** the completed inspection found no suitable identity in `IGitProvider`, tree/blob entries, workspace metadata or semantic-index versions. Implement the owner-issued identity above. If implementation discovers an actual stable identity API, record its exact provider/API/version and executable guarantees for every lifecycle case before proposing reuse. An unsupported hypothetical API is not a dependency that postpones this design. Established File IDs must survive any later provider change.

## 2. Authoritative lifecycle producer

### Source profile and coverage

The minimal first profile is an enrolled, owner-managed canonical repository/ref tree, using the selected registered GitHub or Forgejo adapter. Enrollment binds the exact managed Tenant, owning Folder/source scope and canonical repository/ref internally. It must prove provider-enforced owner-only publishing, protection of the ref against force pushes/deletion and bypass, and control of changes to those protections. Arbitrary writable workspaces and externally writable refs cannot be enrolled under this profile.

Use the existing canonical repository/ref writer lock and fencing semantics. Before provider work, persist an idempotent owner mutation intent with expected File revision, parent token and source heads. Source evidence becomes non-current before the provider can expose a change. The producer then executes the provider mutation, verifies its settled outcome and resulting heads, commits lifecycle facts and publishes covered source progress. `WorkspaceFileMutationAccepted`, staged blobs and operation acknowledgement alone cannot mark a File active/current.

The producer MUST compare actual provider heads and current write/protection authority with the covered source state. Unexpected changes, lost enforcement, uncertain outcomes or a writer capable of bypassing the boundary block that source until explicit owner reconciliation. Head equality cannot detect an unobserved delete/recreate or force-push ABA; enrollment therefore requires the enforced writer boundary as well as head comparisons. Supporting uncontrolled external writes later requires a complete authoritative change source with continuity and gap evidence, as separate work.

Coverage consists of the native source binding identity, its owner stream/checkpoint, the covered provider head(s) internally, completed operation identities and an initialized committed projection generation. Each affected File revision must be covered by that same committed source state. A source binding may reuse its existing native binding-event identity; no public provider address or new revision allocator is needed.

### Required owner commands and events

Each command carries its existing EventStore identity/idempotency metadata, expected native File/source revisions, exact Tenant and parent context. The domain aggregate validates intent; provider work occurs outside pure `Handle`/`Apply`. These names define new domain work, not additional proprietary transport routes.

| Intent → settled event | Required effect |
| --- | --- |
| `CreateFile` → `FileCreated` | Reserve one File ID in the durable intent; confirm a new live provider location; establish incarnation, parent token and internal address. A retry returns the same identity. Adoption of a pre-existing unmanaged file requires separately authorized registration. |
| `EditFile` → `FileEdited` | Require the current File/incarnation and expected revision. Preserve identity/parent token; advance native resource revision after confirmed write. A generic path overwrite cannot decide that it is an edit. |
| `RenameFile` → `FileRenamed` | Explicitly bind old and new location to the same File under one owning Folder. Preserve incarnation/parent token; advance revision and atomically switch current address visibility. |
| `MoveFile` → `FileMoved` | Require current authority for File, source and destination, with both parents in the same Tenant. Preserve File/incarnation; change owning Folder and parent token in one File event. Old-parent bindings become unusable and cannot revive on a move back. |
| `DeleteFile` → `FileDeleted` | Confirm removal and persist a terminal owner tombstone. Never discard birth/terminal identity evidence or allow an old ID to become active again. |
| `ReplaceFile` → old `FileReplaced` plus new `FileCreated` | Explicitly retire the old entity and create a distinct File/incarnation, even at the same path with identical bytes. Record the predecessor/successor relation internally; it grants no redirect for old bindings. |

`FileMutationStarted` makes the affected File/source `Transitioning`; settled events return a surviving File to `Active`, or make it terminal `Deleted`/`Replaced`. These are the only public-source lifecycle meanings; `Transitioning` supplies no usable evidence. A confirmed failed operation can restore the prior active state only after proving no provider change. An unknown outcome remains non-current pending write-side reconciliation.

A cross-source move/replacement is a recoverable coordinated mutation: fence all affected source scopes, persist the transition before either provider write, confirm all outcomes, then publish matching File revisions and source coverage. Until completion, neither a half-move nor a half-replacement is current. No distributed transaction capability is assumed. If supported SDK recovery cannot enforce these visibility rules, that technical capability is a dependency; do not emulate it with raw Dapr/database writes.

Location uniqueness is serialized under the canonical source writer: two active File identities cannot claim the same location in one source tree. Aliased Folders/workspaces resolving to the same canonical repository/ref must share that serialization. A conflicting concurrent create, stale edit, lost fence or ambiguous provider result cannot publish `Active`.

### Persistence and recovery

File facts, owner verification and link registration/revocation are EventStore domain events. Derived identity, location and binding indexes use `IReadModelStore`, `ReadModelWritePolicy` and supported atomic batch/rebuild/recovery APIs. Use one File state transition to change its parent; indexes can disclose only the complete committed generation covering that transition. Read records contain no persisted actor grants. Internal address records must not be loaded or serialized into the content-free result, except within owner policy evaluation.

Duplicate delivery with the same native identity/payload is idempotent. Conflicting duplicates, missing sequences, unknown event versions, head rollback, partial batches and rebuilding/poisoned generations fail closed without advancing a trustworthy checkpoint. Restart/rebuild derives the same File IDs, birth/parent tokens, link instances and tombstones from durable events. Expiring deduplication ledgers are not the sole proof that a terminal identity stays retired. Queries cannot recover pending batches, run reconciliation or create missing records.

## 3. Callable operation contracts

All operations use the supported EventStore domain-service command/query transport and exact enrolled operation types. Authority comes from SDK verification of the exact envelope/query, original actor, originating Projects workload, delegation, Tenant, audience and action. Keep owner-hop identity distinct. Verify/registration are mutations and must also require the existing Projects link action and enrolled binding-writer authority; read authority cannot manufacture bindings. No payload supplies actor, grants, timestamps or trusted head evidence.

The v1 DTOs use lower-camel JSON property names and enum labels, string identifiers, positive signed-64-bit `FileRevision`/`LinkSequence` values, nonnegative native Project applied/head sequences, and UTC RFC 3339 timestamp strings. Native opaque revision/position/generation values are nonempty strings with an explicitly documented source encoding; decimal native sequences use invariant decimal strings there. Empty/zero sentinel evidence is never Current. Read result `schemaVersion` is integer `1`; `outcome` is the string `current`, `stale` or `unavailable`. Only `current` includes `fileIdentity` and `evidence`, and lookup additionally includes `projectLinkIdentity`; reject contradictory combinations and unknown versions. This additive result does not alter the existing numeric `FolderOwnerReadOutcome` serialization.

These logical signatures are the implementation contract; `VerifiedOwnerCall` means SDK-established request context, not a caller-constructible DTO:

```text
VerifyProjectFileTargetAsync(VerifiedOwnerCall, VerifyProjectFileTarget, CancellationToken)
    -> OwnerVerificationResult
RegisterProjectFileLinkAsync(VerifiedOwnerCall, RegisterProjectFileLink, CancellationToken)
    -> ProjectFileLinkRegistrationResult
RevokeProjectFileLinkAsync(VerifiedOwnerCall, RevokeProjectFileLink, CancellationToken)
    -> ProjectFileLinkRegistrationResult
ReadCurrentFileAsync(VerifiedOwnerCall, ReadCurrentFile, CancellationToken)
    -> CurrentFileResult
LookupProjectFileLinkAsync(VerifiedOwnerCall, LookupProjectFileLink, CancellationToken)
    -> ProjectFileLinkLookupResult
```

Tenant is the verified envelope Tenant. File verification/read/lookup target the exact expected parent Folder in the envelope. Binding mutations target their owner binding scope through normal command routing; payload Project and Folder fields must match that scope. Reject unknown/duplicate JSON fields, blank/noncanonical identifiers, contradictory targets and paging; do not reuse permissive legacy path parsing. Apply existing envelope byte/identity bounds and the 128-character reference bound without truncation.

| Request type | Closed payload fields |
| --- | --- |
| `VerifyProjectFileTarget` | `ProjectId`, `FileReferenceId`, `OwnerFileId`, `ExpectedParentFolderId`, `ExpectedFileRevision` |
| `RegisterProjectFileLink` | `ProjectId`, `FileReferenceId`, `LinkInstanceId`, `LinkSequence`, `OwnerVerificationId`, `ExpectedParentFolderId` |
| `RevokeProjectFileLink` | `ProjectId`, `FileReferenceId`, `LinkInstanceId`, `UnlinkEventId`, `UnlinkSequence` |
| `ReadCurrentFile` | `OwnerFileId` (expected parent is the envelope Folder) |
| `LookupProjectFileLink` | `ProjectId`, `FileReferenceId`, `LinkInstanceId` (expected parent is the envelope Folder) |

`ExpectedFileRevision` and supplied event pointers are preconditions/hints, never evidence until compared with native owner facts. Verification selects an existing current File by its owner identity; it cannot create a File or derive one from a path. A caller starting from a path must first obtain a File ID through an explicitly authorized owner mutation/selection flow; the old metadata adapter is insufficient.

`OwnerVerificationResult` has `outcome = verified` with only `ownerVerificationId` after durable commit. `ProjectFileLinkRegistrationResult` has `outcome = registered` or `revoked` after durable commit and returns only the exact `projectLinkIdentity`. Both result types can return `pending` with the native `operationId`, or field-free `stale`/`unavailable`. All use `schemaVersion = 1`; safe denial and invalid/conflicting commands retain existing SDK transport meanings. No mutation result contains File addressing, a permission receipt or a Current grant. Pending work must be resumed by the write workflow, never by lookup.

### Verification → committed Project link → registration

1. Projects authorizes its link action. Folders verifies the exact File's current Tenant, parent, lifecycle, file policy/classification and metadata authority, using section 4. It persists `ProjectFileTargetVerified` with the exact Project/reference, immutable target tuple and observed native revisions. Success is `Verified(OwnerVerificationId)` only after that event commits. The verification pointer carries no reusable Allowed decision and has no invented expiry.
2. The additive new-link workflow commits Projects `FileReferenceLinked` with an optional new `OwnerVerificationId` property set to that exact pointer. Projects' event/read projection preserves the committed envelope `MessageId` and `SequenceNumber` as link-instance provenance. The existing positional event fields and legacy readers must remain compatible. Projects stores an opaque owner-verification reference, not address/content or raw authorization. Old events omit the property and remain interpretable.
3. Projects calls registration with that committed link instance. Folders obtains the authoritative current Project link observation below; it does not trust an event-shaped payload. The observation's Project/reference, link instance, sequence, parent and verification pointer must all match. Folders resolves the durable verification, reauthorizes both sides and rechecks the same File incarnation/parent token and unchanged verified File revision. A changed target requires new explicit verification; registration cannot silently retarget it.
4. `ProjectFileLinkRegistered` commits the full scoped key and immutable target. Only then may the new workflow report a usable owner binding. A Project link committed before registration is not current owner evidence; failures are recoverable pending work, never proof from its old `ReferenceState`. Same registration delivery is idempotent; a different target/pointer for an existing instance conflicts. Registration never overwrites an instance or another Project's binding.

A prepared verification without a committed link is inert. A crash after either commit is resumed by the authorized write workflow with original native IDs. An unlink racing registration wins through current Project-head comparison: late registration cannot reactivate the instance. Cancellation after a command commit does not erase that event; resume idempotently. Query cancellation performs no write.

### Required Projects link observation seam

Projects must supply an authenticated, content-free `ReadCurrentProjectFileLinkAsync` seam (or an equivalent complete owner-event projection plus native head read) for this handoff. It returns only:

```text
ProjectLinkObservation:
  TenantId, ProjectId, FileReferenceId
  LinkInstanceId, LinkSequence, OwnerVerificationId, ParentFolderId
  State: Active | Unlinked | Absent
  AppliedProjectSequence, CurrentProjectHead
  CommittedGeneration, LastVerifiedAt
```

This is a required new seam, not an existing API claim. `Active` requires an exact live link, matching native birth event, current Project lifecycle and current caller Project authority. Every Project event, including unlink/archive/restore and irrelevant events, must be covered through the current head; checkpoints cannot skip unlinks. An unlink event lacks a link-instance payload today, so its native sequence must be folded against the preceding active linked event to retire the correct instance. No new allocator is needed.

For proven `Absent`, link-specific fields are absent and a native head of zero is legitimate only with initialized complete absence proof. `Unlinked` retains the retired instance's native provenance internally. Legacy active observations may have null verification/parent facts; those observations cannot supply a usable binding. They remain Unavailable under the legacy rule below, without changing stored history.

Before disclosing File identity, lookup must obtain a current observation authorizing the caller's exact Project read; registration instead requires its link action. A mirror can prove membership only with authenticated complete coverage equal to the actual Project head, and cannot prove actor permission. Do not load Projects descriptive detail or invoke inspection-audit routes. Missing records without complete coverage are unknown, not proven absent.

### Unlink, lookup and terminal invalidation

An authoritative unlink event retires its instance through `ProjectFileLinkRevoked`. Revocation changes no File state and requires no File-content operation. A delayed owner revocation cannot create a disclosure window: lookup independently requires current Project membership/head equality. Replayed unlink for L1 cannot revoke a later L2; lower sequence cannot replace higher progress.

Lookup first establishes verified caller and Project scope, then exact current link instance and owner binding. The binding projection's applied native owner sequence must equal its actual stream head in a complete committed generation, independently of the Project head. Lookup reads the File source and compares its incarnation, actual parent and parent token with the bound target. All checks in section 4 run again before returning the identity. Bindings are facts about association, never authorization caches.

File delete/replacement and parent changes invalidate lookup immediately through target comparison, even before binding-maintenance events arrive. Such invalidation is permanent for that instance: restoring the old path, bytes or Folder does not restore its birth/parent token. An authorized new link or explicit relink has a new native link instance and owner verification; no implicit redirect to a replacement File occurs.

Existing references without a recoverable verification/binding return non-current Unavailable after Project authorization. They require separately authorized owner-backed re-verification/relinking, never automatic backfill, guessed mapping, history rewriting or NoMatch. The lookup path performs no such work.

## 4. Native evidence and currentness

### Content-free result schema

`CurrentFileResult` and `ProjectFileLinkLookupResult` are closed unions:

| Case | Allowed fields |
| --- | --- |
| `Current` | `FileIdentity`, `Evidence`; lookup additionally returns the exact scoped `ProjectLinkIdentity`. |
| `Stale` | Fixed outcome only. No identity, binding, revisions or usable receipt. |
| `Unavailable` | Fixed outcome only. No identity, binding, revisions or usable receipt. |
| Safe denial | Existing `QueryResult.Failure("safe-denial")`; no result DTO or target-specific reason. |

```text
FileIdentity:
  TenantId, OwnerFileId, FileIncarnationId, ParentFolderId, ParentBindingId
  LifecycleState: Active
ProjectLinkIdentity:
  ProjectId, FileReferenceId, LinkInstanceId, LinkSequence
Evidence:
  FileResourceRevision: ScopedNativeRevision
  SourceCoverage: CommittedCoverage
  PermissionRevisions: nonempty ordered set of ScopedNativeRevision
  BindingCoverage: CommittedCoverage                     # lookup only
  ProjectLinkCoverage: CommittedCoverage                 # lookup only
  LastVerifiedAt, AuthorizedAt
ScopedNativeRevision:
  Authority, TenantId, ScopeKind, ScopeId, RevisionKind, Value
CommittedCoverage:
  Authority, TenantId, ScopeKind, ScopeId, Generation
  AppliedPosition, HeadPosition, ObservedAt
```

Positions/values retain their source-native encoding and are compared only within identical authority/Tenant/scope/kind. `Generation` is the supported committed projection generation, not a new domain counter. Provider heads/addresses remain internal; source coverage exposes the owner's native source progress that certifies them. Scope IDs are opaque, contain no paths or provider addressing, and disclose no unrelated resource. Receipt encoding is deterministic and versioned with the new source contract. No global watermark, hash-as-identity or ad hoc permission version is permitted.

### Required native mappings

| Evidence component | Required source and equality |
| --- | --- |
| File resource | Native File stream sequence and incarnation/parent-event identities; projected File sequence equals its actual stream head. |
| Source coverage | Committed owner source/checkpoint through all settled mutations affecting the File; current provider head/protection verification agrees with that committed source. Missing initialization, transitions and gaps cannot be current. |
| Folder lifecycle/ACL | `FolderPermissionProjection.LastSequence` compared with the exact Folder stream head, preserving the native scope. Active parent and exact File membership are separate requirements. |
| Organization ACL | `OrganizationPermissionProjection.LastSequence` and its exact native stream head, retained independently of Folder revision. Do not reuse rows whose Organization sequence was replaced with zero in the merged snapshot. |
| Tenant access | Native Tenant access/membership/lifecycle revision and current owner head, with projection coverage equality. `ProjectionWatermark`/age alone do not meet this requirement. A supported Tenant owner head seam is a technical dependency if absent. |
| File visibility/classification | Actual owner path-policy, sensitivity and any resource-specific ACL/configuration revision used for this internally resolved File. Cover every policy input, including inherited inputs; fail closed if any lacks source-backed revision/currentness. No new ACL system is implied. |
| Caller authority | SDK-verified originating caller and all native revocation/group/delegation facts used by the decision. Permission claims cannot be borrowed from another actor/workload/delegation or a previous request. Missing supported revocation evidence is a dependency. |
| Owner binding, when used | Exact immutable target and native registration/revocation stream sequence; binding applied sequence equals its actual owner stream head in the same committed generation. |
| Project link, when used | Exact current link fact and covered Project sequence equal to current Project head, plus current caller Project authority; no persisted binding substitutes for either. |

For lookup and registration, the permission tuple also includes every native dependency of Projects' exact read/link authorization. A current Project membership head alone is insufficient permission evidence.

### Two-pass protocol

Use an injected server clock and the propagated request cancellation/deadline. One combined interactive dependency deadline is at most two seconds from request admission, including lookup and all owner checks. No query-level retries. This source also requires its observations to be at most two seconds old at disclosure; a five-minute Tenant budget cannot satisfy it.

1. Verify the SDK context, exact operation/target and current caller scope before protected lookup. Read the exact link/binding, File and complete committed coverage as applicable; resolve location internally only for owner policy checks.
2. Collect each required native head/revision **after** the awaited read completes. Compare projected/applied positions to actual scoped heads. Verify active lifecycle, immutable tuple matches, source protection and exact File metadata purpose/action. Record each actual observation instant, then the actual policy decision time.
3. Immediately before returning, reread all contributing heads/revisions, source coverage/protection and Project membership/permission as applicable. Require equal scoped native tuples and the same committed generation/binding. Reevaluate policy against that stable tuple. Repeated Allowed booleans without revision equality do not pass.
4. Changed tuples, missing coverage, expired/revoked caller authority or exceeded deadline yield no usable evidence. Do not return a previously read identity. `LastVerifiedAt` is the earliest completed native observation underlying the final stable tuple; `AuthorizedAt` is the actual final policy evaluation instant. Require `0 <= disclosureTime - LastVerifiedAt <= 2 seconds` and consistent server times; preserve per-source `ObservedAt`.

This establishes evidence at the verified read cutoff. It promises no validity lease after disclosure; add no expiry field or custom signer. The source's immutable target tokens and monotonic native revisions prevent ABA within the protocol. Any unsupported head, caller or provider-protection seam blocks `Current`; `StrictRead()` alone is insufficient.

### Safe failure and disclosure

Absent, forbidden, cross-Tenant, wrong-parent, unlinked, deleted, replaced, hidden/sensitive and directory targets share safe denial with identical status/body/header projection and no target-specific diagnostics. A non-current source, unmapped authorized legacy link, missing/uninitialized projection, malformed native evidence or unknown head is Unavailable; a proven projection-behind-head condition may be Stale. Neither means a complete empty result or NoMatch.

Malformed envelopes use the existing field-free invalid-envelope behavior only after scope authorization; before it, preserve safe denial. Registration conflicts use existing safe command-conflict semantics without exposing the conflicting File. Normalize dependency failures without reflecting exception/provider data. Propagate cancellation rather than converting it into denial or absence.

No content-free read calls workspace/path metadata endpoints, file-content/range/context APIs, full-detail hydration, semantic search or inspection-audit routes. Internal policy evaluation may use the private current address; it must not retrieve file content. Results/logs/traces/errors exclude paths, names, labels, workspaces, repository/provider addresses, content, sentinels and stored actor grants. Platform-owned security-denial auditing remains permitted. Request authorization evidence is never persisted in the mapping.
