---
title: 'Story 12.2 historical applicability and post-C3 identity authority prerequisite'
status: 'awaiting-owner-capability-and-enrollment'
created: '2026-11-14'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

The independent review-seven corrections can be implemented with the current APIs. Two defects
remain open: a historical native-release/nonterminal/failed task can accept an obsolete coherent
row/evidence/applicability triple, and a new source position can reuse a MessageId after its original
400-day derived identity record expires. Neither a green component suite nor a strict live pass
establishes these missing guarantees. No capability, workload grant or retention change is made by
this proposal.

The current bounded checkpoint retains the active task/workspace digests and the tail source
identity. Original successful task completion is separately retained and now also checked before
duplicate acknowledgment. Native release deliberately does not complete a task. A historical task
without original completion therefore needs independent evidence of its latest applicable source
position. Another mutable copy of its value is insufficient. An absent expired message row likewise
does not prove that the message never occupied a previous source position.

The existing public interfaces are present; this is not a claim that correctness reads are prohibited
because they cost time:

```csharp
Task<NativeSourceObservation?> INativeSourceMetadataReader.ReadAsync(
    AggregateIdentity identity, CancellationToken cancellationToken = default);

Task<AuthoritativeStreamReadResult> IAuthoritativeEventStreamReader.ReadAsync(
    AggregateIdentity identity, CancellationToken cancellationToken = default);

IHttpClientBuilder AddEventStoreGatewayAuthoritativeEventStreamReader(
    IServiceCollection services, string domain, string gatewayAudience = "eventstore");

Task<SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>>
    SharedProjectionEpochCoordinator.ReadManyAsync<TValue>(
        SharedProjectionScope scope, IReadOnlyList<string> logicalKeys,
        CancellationToken cancellationToken = default) where TValue : class;

Task<ReadModelEntry<TValue>> SharedProjectionEpochCoordinator.ReadGenerationAsync<TValue>(
    SharedProjectionScope scope, long generation, string logicalKey,
    CancellationToken cancellationToken = default) where TValue : class;
```

`NativeSourceObservation` contains a committed native prefix proof and an observation instant, with
no event list, per-task latest applicability or MessageId lookup. The independent source-head adapter
returns only its verified sequence. The proof digest cannot recover those missing source facts.
The shared projection APIs pin a selected generation and preserve CAS/lineage; they do not independently
authenticate which source position most recently changed an arbitrary historical domain key.
`MatchesCapturedOwnerPositionAsync(scope, operationId, sourceStream, eventPositions, token)` checks
positions against an active sealed Building capture. Captured identity pages protect original
position/payload identity during SDK delivery/rebuild; they do not expose a current task selector or
a cross-position MessageId uniqueness query. A journal position digest cannot prove that a new
MessageId differs from every expired historical one.

The complete-source reader could reconstruct both facts for histories within its supported bounds,
but the actual reachable production authority does not permit substituting it here:

| Actual caller/path | Existing configuration and boundary |
| --- | --- |
| Ordinary Server | `FoldersServerServiceCollectionExtensions.AddFoldersDomainServices` registers the metadata-only reader. `FoldersDevelopmentOwnerMetadata` adds the full native source reader only inside `DEBUG && FOLDERS_OWNER_METADATA_SOURCE`, with explicit private Development/catalog opt-in. That registration grants no source-range authority. |
| Ordinary Workers and distinct proof Workers | `FoldersWorkersModule` registers the metadata-only reader. No full-source reader or independent actor-state query grant is composed. |
| Ordinary AppHost and strict fixture receiver | AppHost `Program.cs` and `FolderDurabilityAcceptanceFixture` set each actual caller's own `Authentication:BoundStreamRead:PublicKeys`, `AllowedDomains=folders` and `MetadataOnlyCallers=true`. Source credentials and exact resource proofs remain independently required. |
| Actual workload receiver | `StreamsController` refuses an enrolled metadata caller unless `FromSequence=0`, `ToSequence=0`, `PageSize=1`, `RequireNativeProof=true`, and checkpoint/continuation/projection context are absent. A valid signed resource-bound JWT and RSA proof do not waive this range refusal. |
| Human gateway client | `FoldersGatewayHumanBearerHandler` forwards the real human bearer only for admitted mutation/content operations. Ordinary task/status reads and background Workers cannot manufacture one of those authorization markers. The gateway also performs its own stream-read tenant/RBAC authorization. |
| Actor reconstruction and `/replay-state` | Actor-owned repositories fold the command-supplied `DomainServiceCurrentState`. A replay-state callback reconstructs a supplied prefix; it is not an independently authenticated current-source lookup for Workers/status. Existing canonical callback grants run EventStore→domain service, not a new Workers→source/actor operation. Replaying caller-supplied old bytes cannot prove current applicability. |

The existing `FolderOwnerReadQueryHandler` is also insufficient: it accepts only an empty payload,
reauthorizes the originating human actor, and returns permission/native revision evidence. It returns
neither historical task/workspace state nor a MessageId disposition. Current source-only callers have
no configured delegated-owner-query grant. A newly defined source-fact query and its actual caller
enrollment would be an owner prerequisite, rather than an existing costly read that is being skipped.

The existing `AuthoritativeEventStreamReader` further limits a complete observation to 10,000 events
and 16 MiB, with a configured whole-read bound (at most 30 seconds). It cannot qualify the already
required 10,001/32,769-position histories. Raising those limits, removing metadata-only enrollment,
borrowing another caller's credential, reading raw actors/storage, or issuing local replay requests
would not be an authorized correction. Named/shared rebuild already receives authenticated sealed
source, and can repair these derived values, but is not an authority for every later live event/read.

The concrete owner prerequisite is an EventStore-owned supported source-authority composition that
can establish, at one verified current source revision, (a) the latest applicable source fact for an
exact historical task/workspace and (b) whether a candidate MessageId already belongs to a different
source sequence. A bounded authenticated streaming scan with independently verified native prefix
and final source observation is one possible owner design; a source-owned bounded query/proof is
another. The owner must select and qualify the contract rather than Folders inventing a protocol.
If source ranges are selected, the platform/security owner must explicitly enroll each actual
Server/Workers caller for that exact operation and tenant/domain/resource scope. Preserve all
existing metadata-only callers and unrelated grants unless a concrete approved migration replaces
the affected enrollment. Public registration alone does not supply that authority.

Qualification must cover native acquire→release→task B and failed/unknown transitions; rollback of
the old coherent triple; legitimate historical reads and pure rejections; selected/inherited
generations and interleaved source/head changes; original terminal completion and earliest closure;
401-day identity receipt expiry followed by repeated versus new MessageIds; duplicate, conflicting,
gap and unsupported source; 10,001/32,769-position histories with growth; authorization refusal,
cancellation and unavailable proofs; source-authorized named repair and subsequent lawful live
delivery. Retained bytes and original source-time deadlines must remain exact. Existing limits,
SDK CAS/generation/Abort/recovery, leases, actual-call ownership, frozen paired absence and expiry,
human/workload separation and canonical outcomes remain unchanged.

This requests owner capability/enrollment work only. It excludes an unbounded domain ledger or
checkpoint map, another self-attesting row copy, retention extension, source replacement, direct
storage, a local platform protocol, changed deadlines/retries, content-owned work, publication,
package/gitlink/pin changes, deployment, Git mutation, public v2 or general performance redesign.
The two spec tasks stay unchecked. Safe handoff requires an owner decision, exact source/API and
grant inputs, owner positive/refusal/capacity qualification, then current consumer, issuer, matrix
and unchanged strict live qualification with independently audited runtime identities.
