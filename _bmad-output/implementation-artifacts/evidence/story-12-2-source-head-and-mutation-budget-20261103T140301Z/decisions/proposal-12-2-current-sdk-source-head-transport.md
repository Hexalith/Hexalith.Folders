---
title: 'Story 12.2 independent source-head transport prerequisite'
status: 'approved-in-progress'
created: '2026-11-02'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

# Owner decision

2026-11-03: The user replied **approve** to this proposal. This authorizes the bounded SDK-owned
read-only source-head composition, actual Folders source-only workload/key/grant enrollment and
consumer migration described below. First establish the authoritative-zero/absence evidence contract
through the supported authenticated metadata route. If that contract cannot be established within
the proposed bounds, return the precise remaining SDK prerequisite before production implementation.
All listed exclusions, existing source/proof checks, human-command identity semantics and deadlines
remain in force. Prior sealed evidence remains unchanged.

## Supported evidence contract established before production implementation — 2026-11-03

The supported zero observation is an **existing**, valid `Available` native proof with `Exists=true`,
head zero, the canonical seed digest, no head message, and the exact tenant/domain/aggregate binding.
This is authoritative empty-stream evidence, not physical nonexistence inferred from a missing row.
The read-only operation never creates or bootstraps an anchor. A never-created/unactivated aggregate
without supported native evidence remains unavailable. The approved evidence table does not require
turning every physically absent aggregate into safe not-found; it requires preserving valid zero
separately from unsafe or unavailable evidence.

Before any production change in this continuation, eight owner component cases executed the real
`AggregateActor`, production workload authentication with a signed JWT and exact RSA resource proof,
the existing `/api/v1/streams/read/workload` controller and public gateway/native reader. Existing
positive and zero native proofs returned 200 with exact content-free evidence. Missing metadata,
legacy empty/nonempty metadata without proof, completed/pending erasure and malformed native proof
returned 503 and null native observations. Actor inspection still distinguishes their actual
`Exists`, `Erased` and `Unsupported`/`Invalid` states; the safe HTTP outcome does not grant absence
authority. No actor-state write, source event read or payload-readability call occurred; persisted
state serialized identically before/after. These are component credentials and in-memory committed
state through real implementations, not live enrollment/catalog acceptance.

The final owner build passed with zero warnings/errors; the eight cases passed with zero
failures/errors/skips in 1.126 seconds. The initial new-test build's missing null-argument guard is
retained. The narrow supported contract requires no new persistence protocol, actor schema,
proof-bootstrap grant or raw-actor guard exception, so the approved independent composition and
consumer enrollment may proceed with unsupported absence remaining unavailable.

## Metadata-only enrollment enforcement within the approved scope

Executed current-source controls additionally show that the existing generic stream grant permits an
event range using the same resource proof as a metadata request, or using JWT tenant/domain bindings
without that proof. This is established existing generic behavior, not a new vulnerability claim:
the current grant is a stream-read grant. A client wrapper alone cannot enforce the approved
metadata-only authority at the receiver.

The approved least-privilege source-reader capability therefore includes one explicit trusted
receiver enrollment setting for the new source-only callers. For those callers the existing route
must require exactly `FromSequence=0`, `ToSequence=0`, `PageSize=1`, `RequireNativeProof=true`, no
checkpoint/continuation/projection context, and an actual valid exact resource proof even when JWT
tenant/domain claims could otherwise authorize. The setting belongs to supported host/catalog
configuration, never a request flag. Generic readers retain their existing behavior; route, proof,
JWT, actor and persistence schemas remain unchanged. Ranges, altered shapes, missing/invalid proof,
mixed/wrong credentials and bypass attempts must deny before actor/source observation. This is the
necessary implementation of the already-approved metadata-only enrollment, not an additional grant,
raw-actor exception or authority expansion. Record its binding in the independent catalog observation.

Before that correction, both real actor/workload HTTP range controls passed with 200 and one original
event returned (two cases), and three public proof controls verified that range/page-size/native-proof
shape is not part of the existing resource signature while changed caller/aggregate still refuses.
The complete new owner matrix passed 10/10; current client native-reader/proof controls passed as
recorded in the continuation evidence. No production source was changed before these reproductions.

# Approved bounded scope

Authorize the EventStore owner to qualify a public, independently enrolled, read-only source-head
composition, then migrate the existing Folders independent source-head reader to that composition.
The operation must preserve authoritative zero/absence separately from unsupported, missing-proof,
erased, malformed and unavailable evidence. The owner must first define which supported source
evidence can establish zero for a never-created aggregate. If that distinction cannot be established
through the existing authenticated metadata route, return the remaining precise SDK prerequisite
before implementation. Do not infer zero from a 404, 503, null proof or missing projection.

This proposal supplements the approved
[security enrollment](proposal-12-2-current-sdk-security-enrollment.md) covers its listed endpoint,
catalog, authentication and probe changes and a selective public registration compatibility repair.
A separately keyed source reader, workload/key/grant enrollment and authoritative absence semantics
are a distinct capability now authorized by the owner decision above. No production implementation
or enrollment existed at approval. The mandatory raw actor/catalog guard remains intact.

# Established current-source boundary

The parent's fourth audited strict operational attempt passed genuine bound bootstrap: actual 202,
terminal Completed, all four original credential/proof refusals, original retained source and
projected administrator authority. Tenant creation, tenant membership and organization baseline/grant
commands completed. The unchanged body then issued its first unknown-profile
`ConfigureProviderBinding` PUT, which requires 403. The client instead observed
`HttpRequestException` / `HttpIOException: ResponseEnded` at
`OperationalStatusDurabilityAcceptanceTests.RequireRealTaskClosureAsync` line 173. This was the
provider-binding refusal control; it was not folder creation. The attempt finished with one failure,
zero errors/skips in 233.011 seconds. SM4 was not reached.

The retained log independently demonstrates the following source-read failure during that command:

1. Folders invokes `DaprOrganizationStreamSequenceReader` for the organization aggregate while
   processing the different provider-binding aggregate.
2. The reader constructs a raw Dapr `AggregateActor/.../GetCurrentSequenceAsync` invocation.
3. The current mandatory `RouteCatalogInvocationHandler` refuses it with
   `Raw actor invocation has no proven typed catalog scope.` before catalog authority acquisition or
   network transport.
4. The domain `/process` path returns 503; the actor records the command as Rejected.

The phrase occurs in three fields of **one structured refusal event**, not three established
requests. `/process` completes its 503 response and the actor records Rejected, but the retained
log has no corresponding completed outer public PUT response before the client's premature EOF.
No retained stack or response-framing observation connects that EOF to this refusal. The source-read
incompatibility is proven; the separate `ResponseEnded` cause remains unresolved. No timeout,
retry, response-framing or assertion change is justified by this evidence.

The owner packet retains exact private logs/XML, audited runtime/source/head identities and empty
owned-resource cleanup for `strict-operational-4`. No further live run was made for this proposal.

# Existing public capabilities and limits

The Folders reader is registered in both Server and Workers. It supplies fresh organization/folder
heads to authorization, provider-binding and operational readers. Its exact own-actor sequence scopes
avoid self-invocation deadlock only when tenant and aggregate match; a provider-binding actor's head
cannot stand in for the different organization's head. A projection watermark cannot replace that
fresh source observation either.

The current SDK exposes workload stream reads, `BoundStreamReadProof`, workload assertion sources
and `GatewayNativeSourceMetadataReader`. The latter requests content-free metadata (`FromSequence=0`,
`ToSequence=0`, `PageSize=1`, `RequireNativeProof=true`) and validates native proof, but returns null
for missing, unsupported and unavailable evidence. The current missing-actor proof inspection does
not establish authoritative zero: it reports unsupported/missing proof, and required-proof gateway
reads fail closed. These outcomes cannot satisfy the existing `Task<long>` reader's zero contract by
conversion.

`AddEventStoreNativeSourceMetadataReader` currently selects the SDK's raw Dapr metadata adapter; that
adapter is refused by the same mandatory catalog guard. Globally selecting
`AddEventStoreGatewayWorkloadTransport` changes the default gateway transport and assertion source.
Folders' current default gateway forwards the real human caller for commands and deliberately rejects
background/domain-service human forwarding. Changing that default would alter existing command
identity semantics. Public constructors permit explicit composition, so this is not a claim that no
client can be constructed. The missing approved contract is a supported, independently configured
source-only composition, real enrollment and preservation of the required source distinctions.

The present AppHost/fixture has no independently enrolled Folders source-reader workload, signing key
and least-privilege grant. Reusing or relabeling EventStore or Tenants' existing identity/key would not
establish Folders authority and is excluded.

# Proposed bounded implementation after approval

The EventStore owner should prefer the existing authenticated workload metadata route and public
proof types if they can meet the source contract. Add only the smallest public SDK registration or
client helper needed to select an independently configured read-only client without changing the
default human-command gateway. Specify the result contract and distinguish valid positive head and
authoritative zero/absence from unsupported or unsafe evidence before writing the consumer adapter.
No new persistence protocol, actor schema, generic raw actor permission or internal registration in
Folders is proposed.

Enroll the actual Folders caller with its own issuer client and signing material through the supported
SDK/issuer/catalog configuration. Bind every request to its exact caller, managed tenant, `folders`
domain, aggregate resource, stream-read operation, expiry and current committed catalog. Permit only
the metadata observation required by the existing reader; do not add command-submit or broader domain
grants. Inventory actual Server/Workers consumers before selecting host enrollments. Give Workers an
independent grant only if its existing reachable reads require it; do not grant both hosts by default.
Keep secrets in disposable/private configuration and record only safe identities and hashes.

In Folders, adapt the existing independent source-head seam through that public SDK composition.
Preserve the exact own-actor shortcuts, every fresh observation, cancellation and the existing
three-second reader ceiling and all enclosing five-/forty-second limits. A stricter SDK bound may
remain stricter; no deadline may be increased. Preserve current authorization outcomes:

| Evidence | Required existing consumer disposition |
| --- | --- |
| Valid independently observed positive head with missing projection | Stale/unavailable; never authorize from absent projection |
| Positively established zero head/authoritative absence with missing projection | Existing safe not-found outcome |
| Unsupported, missing proof, malformed/corrupt, erased, denied, catalog mismatch or unavailable source | Unavailable/refused; never invent zero or authorize |
| Exact current own-actor tenant/aggregate scope | Existing scoped actor head; no independent self-call |
| Different tenant or aggregate | Fresh independently authorized observation required |

No direct state-store reads, fabricated scopes, caller impersonation, projected-head substitution,
guard bypass, weakened human/workload separation, relaxed denial, changed deadline, changed strict
body, public v2 exposure, provider execution, package/gitlink update, publication, deployment or Git
mutation is included.

# Executed refusal and distinction controls

`CurrentSdkSourceHeadBoundaryTests` contains seven passing component cases using the actual public
SDK catalog handler and actual Folders/SDK reader implementations. They establish:

- The Folders raw source-head adapter is refused before either transport or catalog authority is used.
- The SDK's registered raw metadata adapter also fails closed with zero transport/authority calls.
- A missing organization projection plus that refused source read remains unavailable with
  `organization_stream_unavailable`; it never becomes a zero-head/not-found observation.
- An explicitly supplied zero source observation retains the existing safe
  `organization_projection_missing` not-found outcome.
- A gateway missing response, unsupported zero without native proof and backend unavailability all
  remain null observations; each request retains the exact metadata-only native-proof shape.

Component dependencies deliberately supply these source/gateway outcomes. They are not live proof of
authoritative absence, workload enrollment, successful metadata transport or a fixed outer EOF.
Environment mutation is isolated in a nonparallel test collection. No product guard is replaced.

Exact focused commands (Debug, current default versions, source project references):

```text
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false -v:quiet
dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.CurrentSdkSourceHeadBoundaryTests -result-xml /tmp/story12-2-source-head-boundary-tests.xml
git diff --check
```

The build passed with zero warnings/errors; tests passed 7/7, zero failures/errors/skips, 0.184 seconds;
diff checking passed. The initial diagnostic-only build failed on an undeclared mocking-library using;
the tests were corrected to use a local counted `DispatchProxy` without adding a package. Both build
logs are retained. This phase added only diagnostic tests and this proposal/checkpoint.

# Qualification required after owner approval

1. Exercise production public registration with the existing human-command gateway intact. Qualify
   real issuer credentials, exact source proof and committed catalog binding; deny wrong caller,
   tenant, domain, resource, operation, mixed credential, missing/expired/revoked assertion, bad proof
   and stale/mismatched catalog before source observation/effects.
2. Prove positive head and authoritative zero/absence through the selected supported source route.
   Preserve separate unsupported, missing-proof, erased, corrupt, denied and backend-failure results.
   Test missing projections and retained/legacy empty histories without synthesizing authority. Assert
   no source/event mutation, resurrection or authorization on refusal.
3. Qualify exact own-actor shortcuts and different-aggregate reads, including provider-binding to
   organization reads, without self-deadlock, guard bypass or projected-head substitution. Preserve
   fresh final observations, cancellation and every existing deadline.
4. Run focused owner SDK and consumer tests, then the affected current-source Server, Workers,
   Integration, Gateway and Contracts/conformance checks individually with audited project references.
5. Execute the unchanged strict authenticated real-provider class on final rebuilt/audited outputs,
   retaining all bootstrap/source/refusal assertions and every prior failure. Independently resolve
   the outer `ResponseEnded` cause if it recurs; a source-read repair alone is not proof of that fix.
   All six durability concerns and SM4 require their original evidence.

Story/sprint remain in-progress and the original live acceptance row stays unchecked. Broad SDK,
Release/package/PD10, capacity identity validity and independent whole-story review retain their
separate open dispositions. Earlier accepted compatibility fixes remain bounded by their own focused
and audited live evidence; no whole-story acceptance follows from them or from these seven controls.

# Approved implementation and focused handoff — 2026-11-03

The SDK now exposes `AddEventStoreGatewayNativeSourceMetadataReader(domain)` as an independent
named metadata client. It composes the existing workload route and RSA resource proof with the
host's own configured issuer identity/key, exact Dapr endpoint and selected physical gateway AppId.
It does not change the default gateway's options, human forwarding or registered credential sources.
Only the configured managed domain can be observed; no command/status client is exposed by the seam.
The existing native-reader two-second bound and the consumer's three-second bound remain intact.

For explicitly configured `Authentication:BoundStreamRead:MetadataOnlyCallers:<caller>`, the receiver
now requires the exact metadata shape and actual valid resource proof before creating an actor proxy.
Resource-bound JWTs cannot waive that proof. The restriction is retained in the independently captured
authentication profile; existing generic stream readers retain their prior behavior. No route, JWT,
proof, actor or persistence schema changed. No raw invocation exception, source-proof bootstrap or
source seed is added. Existing supported native zero is still distinct from unsupported physical absence.

The reachable consumer inventory justified both host enrollments:

| Host | Reachable independent source-head reads | Enrollment |
| --- | --- | --- |
| Folders Server | Organization/folder authorization; provider binding/readiness validation and rebuild; lifecycle/workspace/lock/cleanup/task readers | Own issuer client and RSA key; `folders` metadata only |
| Folders Workers | Semantic-indexing policy's tenant/folder permission evidence reaches the same organization/folder authorization readers | Separate own issuer client and RSA key; `folders` metadata only |
| Disposable replicas | Same physical AppId replicas share that actual workload; proof Workers with distinct physical AppIds reach the same policy | Each distinct actual caller gets its own client secret and key |

The ordinary AppHost realm declares only stream-read scopes for the new service clients, without
human/admin/command permissions. Secrets and signing keys are secret Aspire parameters. The disposable
fixture imports the exact actual caller IDs, selects the isolated gateway, and configures matching
public keys, domain restriction and metadata-only enrollment before catalog capture/startup. Existing
caller membership, including Tenants bootstrap, is merged using the public SDK allowlist helper; its
historical trusted-effect name grants no operation. No numeric-slot replacement discards an existing
caller. The real realm/model controls retain human identities and assert source-service separation.
An already-persistent local Keycloak realm needs the usual explicit issuer enrollment/update; this
source change does not overwrite an existing realm or claim production deployment.

The Folders adapter now consumes the public native metadata reader. It retains exact own-actor scope
shortcuts, validates returned proof identity/erasure/digest, never converts null into zero, propagates
caller cancellation and bounds noncooperative asynchronous reads by the existing ceiling. Existing
transport-specific unit fixtures were migrated to the supported SDK HTTP route; the prior raw SDK
adapter refusal control remains. Strict/capacity test bodies and all domain denial assertions remain.

Final focused results, each run individually on Debug/source references with current default versions:

| Selection | Passed | Errors / failed / skipped |
| --- | ---: | --- |
| SDK Server native evidence, metadata-only enrollment, catalog capture, existing stream controller/metadata/prefix controls | 131 | 0 / 0 / 0 |
| SDK DomainService independent metadata composition and existing workload transport | 15 | 0 / 0 / 0 |
| SDK Client native reader and existing resource-proof scope contract | 62 | 0 / 0 / 0 |
| Folders Server source-head, authority-reader and production command-wire controls | 84 | 0 / 0 / 0 |
| Folders Workers production host security control | 1 | 0 / 0 / 0 |
| AppHost actual realm/model, bootstrap evidence and scope controls | 41 | 0 / 0 / 0 |

All final builds passed with zero warnings/errors; both repository diff checks passed. Intermediate
diagnostic compile/assets failures and the AppHost profile-inventory failure remain retained. That
inventory was corrected for the newly approved source-service clients while retaining human UI and
owner service separation; the final 41-case group passed. Earlier successful overlapping subsets are
not additional cases in the 334-case final selection.

Outputs were released to the parent with no active builds/tests/topology, followed by no further
product/test edits or builds. Exact commands, phase logs/XML, source/output hashes and changed-file
inventory are in the new continuation handoff. The parent owns final runtime rebuilding, broader
regressions, source/runtime audits, pending source-inventory refresh, unchanged live acceptance and
new packet sealing. No live run was executed by this implementation continuation. `ResponseEnded`
remains independently unresolved; source transport correction alone does not establish its cause.
