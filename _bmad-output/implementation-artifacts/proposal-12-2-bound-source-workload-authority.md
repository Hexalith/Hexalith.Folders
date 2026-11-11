---
title: 'Story 12.2 current source-reader workload issuance prerequisite'
status: 'awaiting-owner-input'
created: '2026-11-11'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

# Remaining prerequisite

The current SDK source reader requires a workload assertion signed for the exact tenant and
`folders` domain. The configured disposable Keycloak clients supply ordinary client-credentials
audience/operation scopes. They do not configure the SDK's supported resource-binding issuance
profile. The actual Folders issuer therefore refuses the source request with
`bindings-unsupported`, before a token request or source transport can occur.

Existing approvals already authorize each actual Folders caller's own workload credentials,
RSA source proof, metadata-only receiver grant and supported public composition. They remain
sufficient for local wiring to an existing approved authority; no repeat approval is requested
for that work. What is missing is an actual authority that issues these signed bindings and its
owner-provisioned trust/grant inputs. None is identified by the current fixture, ordinary AppHost
configuration or the inspected root-declared Platform source/docs.

The owner can provide an existing conforming resource-binding authority and its protected
configuration. If no such service exists, its implementation/enrollment belongs to the external
platform/identity owner and requires that owner's concrete capability decision. This proposal
does not authorize creating a new issuer, declaring an arbitrary signer trusted, or enabling a
nonconforming issuer inside the acceptance topology.

# Verified boundary

- Root: `74e95703c1260c61587b5600823a0ac14a6e517d`; EventStore:
  `c94e6fffaf2d689c94bb288f151a65e10a2fea82`.
- `DomainServiceNativeSourceCredentials.IssueAsync` requests exactly
  `eventstore:bound-tenant` and `eventstore:bound-domain`, alongside receiving audience
  `eventstore` and operation `eventstore:stream-read`. RSA `BoundStreamReadProof` is an
  independent additional requirement.
- `JwtWorkloadAssertionIssuer.IssueAsync` refuses those bindings when the configured authority
  profile is absent. The retained November 11 strict run logs that refusal in the Folders
  process at `11:36:18.980Z`; its first provider-configuration actor records Rejected at
  `11:36:19.089Z`, after 617.112 ms. The outer request later exhausts its unchanged retry
  budget. This identifies a concrete unsupported issuance path, without attributing every
  earlier catalog warning, projection warning or failed run to it.
- New Server/Workers-identity controls use the actual issuer and public SDK metadata client
  with valid independently generated RSA proof material and ordinary authority credentials.
  Both refuse without token/source HTTP calls. The real consumer preserves
  `Unavailable/organization_stream_unavailable`, null watermark and no fabricated zero.
  Existing positive composition controls still assert exact tenant/domain assertion requests.

The owning SDK contract is
[`docs/security/workload-binding-authority.md`](../../references/Hexalith.EventStore/docs/security/workload-binding-authority.md).
It states that production workload issuance belongs to the external platform/identity owner
and that conforming HTTP hosts refuse embedded-authority activation in every environment.
The SDK's retained embedded issuer is restricted to explicitly nonconforming Development
protocol fixtures and supplies no host-conformance/runtime-acceptance authority. Enabling
`EnableResourceBindingRequests` alone establishes neither a grant nor enrollment.

# Exact authority contract and required inputs

The external authority must support the existing bounded OAuth client-credentials
`authorization_details` profile `https://hexalith.com/security/workload-binding/v1`.
It must validate the current server-owned client grant and exact requested audience, operation
and tenant/domain bindings, and sign only that admitted scope. The SDK independently validates
signature, issuer, lifetime (at most 120 seconds), exact caller, audience, operation and the
complete binding set before attaching a token; the receiver validates it again.

Required owner inputs, delivered privately rather than in this proposal:

| Input | Required scope |
| --- | --- |
| Authority artifact/owner | An existing externally owned conforming implementation and the repository/deployment owner responsible for it; no such implementation is supplied by the inspected Folders/Platform graph. |
| Trust | Exact HTTPS issuer, discovery/token/JWKS addresses, validated TLS trust and protected approved signer reference/public key identity. Preserve the existing human OIDC issuer separately. |
| Client grants | One own client/secret and actual physical workload identity for Server, Workers and each distinct proof Worker. No borrowed Tenants/EventStore identity or human/admin grant. |
| Source-specific grant | Audience `eventstore`; operation `eventstore:stream-read`; exact managed tenant and domain `folders`; current grant identity/revision/validity/revocation. No wildcard or additional operation in this grant. Other existing traffic retains its own distinct grants below. |
| Acceptance target | The approved disposable environment and supported protected configuration delivery/cleanup. No production publication or deployment is implied. |

# Shared issuer and receiver impact

The current public metadata and authoritative-source registrations resolve the host-wide
`IWorkloadAssertionIssuer`; no independently configured source-only issuer is exposed by either
registration. The dedicated workload JWT contract is also shared between outbound issuance
and inbound workload validation. It selects one complete authority contract, without merging
the old public issuer. Therefore changing only Server/Workers' option or gateway receiver trust
would break existing calls. A source-only grant cannot replace the rest of this graph.

The following inventory comes from the actual realm, fixture configuration and SDK outbound
handlers. These are preservation requirements, not permission to add a currently absent grant:

| Existing path | Required separate authority enrollment when migrating shared trust |
| --- | --- |
| Folders Server and distinct Workers -> EventStore metadata source | The source-specific tenant/domain-bound stream-read grant above. The Server's selected authoritative full-source client uses the same host issuer and its existing source authorization; do not silently broaden the metadata-only receiver grant or remove its restrictions. |
| EventStore -> actual Folders and Tenants canonical callbacks | Preserve each actual physical receiving audience and the existing configured `domain-service:process`, `domain-service:replay-state`, `domain-service:project`, `domain-service:metadata` operations. Ordinary unbound requests need explicitly scoped `AllowUnbound` grants because enabling the profile also moves unbound requests onto it. No wildcard or unhosted-sample enrollment. |
| EventStore -> Folders/Tenants `/query` | Preserve exact query-envelope authority bindings. `DomainServiceWorkloadAssertionHandler` forwards `QueryCallerAuthorityBinding`'s tenant, domain and `eventstore:bound-query-authority-v1` digest. The external profile must support the existing default-off `AllowQueryAuthorityDigest` capability for each exact audience/domain/tenant query grant. An unbound query token cannot substitute. |
| Tenants -> EventStore bound administrator bootstrap and native administrator source | The fixture's existing actual Tenants client uses `eventstore:command-submit` and `eventstore:stream-read` with its existing independent RSA command/source proofs. Preserve its actual caller and existing `tenants`/`global-administrators` scope checks. Its existing unbound assertion acquisitions need distinct exact `AllowUnbound` grants; neither bootstrap nor its proofs may be replaced. |
| Human -> Folders -> EventStore ordinary commands/status | Keep the existing human bearer/issuer, canonical body and status headers. These do not become workload requests or receive new service privileges. |

The current Folders clients have no configured command, owner-query or trusted-effect operation
scopes; this proposal does not add them. No Folders call site of `DelegatedOwnerQueryClient` or
trusted-effect submission was found in the inspected source. Generic SDK availability is not
an enrollment requirement. Any complete-command source-bound capability, projection PubSub
provenance, or other newly selected traffic needs its own exact existing contract and an
explicit justified inventory before it can join the migration.

All affected actual inbound receivers must trust the same selected external workload issuer
before switching the associated callers. EventStore, Tenants, Folders Server and the actual
Workers therefore form the bounded minimum trust migration; preserve every already configured
caller/operation and catalog capture. Memories is not JWT-wired by the ordinary AppHost and
has no identified dependency on this source-reader issuance change; do not silently enroll it.
No full trust migration is implemented here. The authority owner must confirm these distinct
grants and issuer compatibility as a concrete prerequisite before local wiring can proceed.

# Bounded consumption once those inputs exist

1. In the owning authority repository, enroll only the grants above, or implement the missing
   exact profile only after its owner approves that separate capability. Keep signer custody,
   TLS trust, grants and revocation with that owner; do not add an issuer to Folders or bypass
   the SDK's conforming-host boundary.
2. In Folders `src/Hexalith.Folders.AppHost/Program.cs` and
   `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`, consume that
   authority through the existing dedicated `Authentication:WorkloadJwtBearer` contract,
   `Authentication:WorkloadIssuer` settings and protected parameters. Migrate the shared trust
   cohort and every separately exact existing grant in the table above, including required
   `AllowUnbound` and query-authority digest support. Enable the existing resource-binding
   option only together with verified actual enrollment. Preserve the human
   gateway, own RSA keys, metadata-only receiver enrollment and exact physical Dapr targets.
   Receiver trust changes must include every affected actual runtime host before catalog capture.
3. Add focused actual-issuer positive/refusal controls for exact signed tenant/domain bindings,
   wrong client/caller/tenant/domain/audience/operation, omitted/extra bindings, stale/revoked
   grants, malformed/expired/signature-invalid tokens, caller cancellation and no source
   transport on refusal. Qualify real TLS/issuer trust separately from component test signers.
4. Rebuild/audit coherent Debug/source runtime, execute affected focused consumers and the
   unchanged strict real-provider class. Require real current source/Workers evidence and the
   original 403 control; keep all failures and exact owned cleanup. Historical live passes do
   not qualify the new authority selection.

No implementation/enrollment is performed by this proposal. Preserve strict d29 and both
companion/capacity bodies, frozen intent, all proofs/final observations, independent unsupported
absence refusal, source-time retention, the approved two-second mutation budget and every other
deadline/retry/shutdown limit. Do not edit SDK guards, substitute an unbound JWT or proof-only
transport, infer source zero, expand authority, change dependency pins, publish, deploy or mutate
Git. SDK privacy, PD10/governance, SM4 and independent review remain separate gates.
