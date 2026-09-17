# Technology Currency and Reality Review — 2026-09-17

## Gate verdict

**CHANGES REQUIRED.** The chosen stack is viable and the architecture is substantially more honest about target
versus as-built state, but six high-severity technology/reality gaps still prevent the document from truthfully
claiming that every downstream-blocking mechanism is decided. The largest issue is ownership: three required
capabilities live in `Hexalith.EventStore`, are absent from that platform today, and are assigned only to Folders
stories. The execution graph therefore cannot deliver the stated design without unrecorded cross-repository work.

No production-code change was made by this review.

## Evidence checked

- Governing inputs: the approved September 15 proposal, September 16 reconciliation, and current implementation
  readiness report.
- Target artifacts: `_bmad-output/planning-artifacts/architecture.md`,
  `docs/deployment/supported-mvp-profile.md`, `docs/runbooks/backup-restore.md`, and the September 17 downstream
  reconciliation.
- As-built Folders seams: AppHost resource IDs, provider HTTP handlers, Server authorization registration and
  endpoint mapping, production startup assertion, and current Dapr component YAML.
- As-built EventStore seams: `AggregateActor`, `EventPersister`, `AggregateReplayer`, command-concurrency options,
  PostgreSQL live-sidecar fixture/component, payload-protection engine, crypto-shredding/restore-admission contracts,
  and backup command implementation.
- Repository persistence authority: `references/Hexalith.AI.Tools/hexalith-state-instructions.md` — Folders domain
  data must be persisted through `Hexalith.EventStore`; direct database, raw Dapr-state, file, or blob persistence
  is forbidden.
- Current primary references: Dapr actors provide turn-based access and transactional actor state
  ([Dapr actors overview](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/));
  PostgreSQL v1 and v2 support transactions, ETags, and actors, while v2 is recommended for new applications and
  is not storage-compatible with v1
  ([Dapr PostgreSQL v2](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/),
  [state-store capability table](https://docs.dapr.io/reference/components-reference/supported-state-stores/));
  ASP.NET Core fallback authorization protects endpoints lacking explicit metadata
  ([Microsoft ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0));
  Dapr authenticates sidecar-to-application callbacks with the app API token
  ([Dapr app API token](https://docs.dapr.io/operations/security/app-api-token/)); and .NET supports a pinned
  connection with original-host SNI through `SocketsHttpHandler.ConnectCallback`
  ([Microsoft .NET SNI guidance](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-sni)).

## What is technically sound

- **Dapr actor plus PostgreSQL is a viable production write substrate.** `AggregateActor` is real, stages events
  and aggregate metadata through `IActorStateManager`, and commits the actor batch through `SaveStateAsync`.
  EventStore already carries a PostgreSQL actor-state component and a multi-process live-sidecar fixture. D-3 and
  D-11 are therefore compatible with the platform rather than speculative technology choices.
- **Fallback authorization is the correct ASP.NET Core primitive.** The current Folders Server calls
  `AddAuthorization()` without setting `FallbackPolicy`, so S-10 is accurately a target rather than an as-built
  claim.
- **The core SSRF connection design is implementable.** `ConnectCallback`, disabled automatic redirects,
  connect-time address pinning, and original-host TLS/SNI can be implemented for both the Forgejo and Octokit
  handlers. The current factories do not implement that policy, consistent with Story 13.1 being pending.
- **AES-256-GCM is compatible with the EventStore platform.** The EventStore submodule already contains a bounded
  AES-GCM payload-protection engine with authenticated data and explicit buffer clearing. A sealed operational-value
  capability can reuse platform cryptographic machinery; Folders does not need or have permission to create a
  second persistence path.
- **Stable app IDs are real.** `FoldersAspireModule` already declares `eventstore`, `tenants`, `folders`,
  `folders-workers`, `folders-ui`, and `memories`, matching I-4 and the supported-profile document.
- **Target/as-built honesty is generally strong.** PD8, PD10, PD11, I-8, production manifests, backup automation,
  and release evidence are all described as target or pending. The remaining findings are places where the target
  itself is still underspecified or assigned to an owner that cannot implement it within the stated boundary.

## Critical findings

None. The execution freeze remains in force, so the gaps below have not yet escaped into a production claim.

## High findings

### TECH-H1 — Platform-owned prerequisites are absent from the execution graph

**Evidence.** Architecture D-12 requires an EventStore read-path upcaster chain; S-6/PD8 names a new
`ISealedOperationalValueStore`; I-11 requires signed deletion/hold export plus safe restore admission
(`architecture.md:686`, `:699`, `:705`, `:811`). The downstream reconciliation assigns these outcomes only to
Folders Stories 12.1/12.2, 12.7, and 13.7 (`reconcile-architecture-downstream-2026-09-17.md:51-82`). In the actual
EventStore source there is no upcaster or sealed-operational-value contract. Its backup trigger/validate/restore
methods explicitly return deferred results, and its restored-backup admission remains `DeferredValidation` until a
physical backup engine exists (`DaprBackupCommandService.cs:73-99`, `:230-260`; EventStore payload-protection guide
`:226-241`, `:288-308`).

**Impact.** A Folders story cannot implement a missing shared platform capability locally without violating the
Hexalith baseline. As written, the rank graph says these Folders stories are executable while silently depending on
unranked work in another repository. This is exactly the kind of downstream invention the architecture update is
meant to remove.

**Required correction — discuss/escalate, then encode.** Add three explicit EventStore-platform prerequisites (or
cite already-approved EventStore story IDs and release versions if they exist):

1. payload schema-version/upcaster registry before Folders 12.1/12.2;
2. sealed operational-value storage/unsealing/key lifecycle before Folders 12.7/12.4; and
3. physical backup manifest, WORM safety-export, and restored-backup admission enforcement before Folders 13.7.

Give each an owner, completion evidence, and a lower execution rank or external-prerequisite node. Folders stories
then own integration and acceptance evidence only. If an EventStore owner or story ID is not available, record that
precisely as a human/platform escalation rather than claiming the schedule is satisfiable.

### TECH-H2 — D-12 does not define the persisted event-version key or its relation to EventStore metadata

**Evidence.** D-12 says every event carries `schemaVersion` and a stable event type, but does not say where that
version is stored or what identifies the logical event (`architecture.md:686`). The current EventStore persists
`EventTypeName` plus `MetadataVersion: 1`; `EventTypeName` defaults to a CLR type name, and `MetadataVersion` is the
envelope-metadata version, not a domain-payload schema version (`EventPersister.cs:97-131`). `AggregateReplayer`
validates only `MetadataVersion`, resolves an Apply method from `EventTypeName`, and directly deserializes current
JSON; there is no upcaster seam (`AggregateReplayer.cs:82-129` and the following deserialization path).

**Impact.** Independent EventStore and Folders implementers can make incompatible choices: put `schemaVersion` in
the domain JSON, add it to the fixed envelope, put it in extensions, or encode it in the type name. They can also
key upcasters on a rename-prone CLR full name. Historical replay remains blocked even if both teams believe they
implemented D-12.

**Required correction — autofix.** Bind one persisted representation and registry key. For example: a positive
`eventPayloadSchemaVersion` in EventStore-owned envelope metadata/extensions, distinct from `MetadataVersion`, and
a stable logical event identifier independent of CLR assembly/type renames. Define the upcaster key as at least
`(domain, logicalEventType, fromPayloadVersion)`, its order as unprotect/authenticate → validate metadata → pure
upcast chain → deserialize/current Apply or projection dispatch, and fail closed on missing/duplicate/future chains.
The platform prerequisite in TECH-H1 must own the envelope/registry change; Folders supplies registrations and
retained fixtures.

### TECH-H3 — PD8 token rotation can change the canonical writer identity

**Evidence.** S-6 defines the lock/correlation token as a deterministic HMAC under a per-tenant, key-versioned key
and says the token keeps every writer on one identity (`architecture.md:699`). PD8 then permits active-plus-previous
keys and authenticated resealing on rotation (`architecture.md:705`). Story 12.7 repeats “token-versioned HMAC” and
rotation/resealing without defining whether an existing binding's token may be recomputed
(`reconcile-architecture-downstream-2026-09-17.md:51-59`).

**Impact.** The same confidential value HMACed under a new key version produces a different token. If any writer
recomputes after rotation while another uses the persisted old token, the architecture's canonical lock identity
splits. Active-plus-previous key availability does not itself provide a canonical choice. AES-GCM also requires an
explicit nonce/DEK uniqueness invariant in the new sealed-value store; naming the primitive alone is insufficient.

**Required correction — autofix.** State one rotation invariant. The lowest-drift choice is: mint the token once
when the authorized binding is created, persist its `tokenKeyVersion`, and reuse that exact opaque token for the
binding's lifetime; rotation reseals ciphertext but never retokenizes a live binding. Token-key retirement is
blocked while live bindings require that version. If retokenization is desired instead, define an atomic alias and
lock-identity migration protocol. Require the sealed-value capability to reuse the EventStore protection engine or
otherwise prove unique nonce-per-key (or unique DEK-per-envelope), authenticated context, zeroization, and
rotation/restart fixtures.

### TECH-H4 — S-9 allows credential-bearing redirects without an origin rule and leaves proxy bypass open

**Evidence.** S-9 validates every redirect destination but permits explicitly followed redirects after validation
(`architecture.md:702`). A redirect from an approved provider host to an attacker-controlled *public* HTTPS host
passes the listed SSRF address checks. The rule does not say whether provider Authorization headers are stripped,
whether cross-origin redirects are forbidden, or whether `SocketsHttpHandler.UseProxy` is disabled. A proxy can
also make `ConnectCallback` connect to the proxy rather than the validated target. The current Forgejo and Octokit
handlers are separate, so a shared policy must demonstrably reach both.

**Impact.** The design can prevent private-network SSRF yet still exfiltrate provider credentials to a public
redirect target or silently bypass address pinning through an ambient proxy. This is a release-security property,
not an implementation detail.

**Required correction — autofix.** For credential-bearing calls, reject cross-origin redirects by default (scheme,
canonical host, and effective port); allow a provider-specific alternate origin only through the same
Security-approved endpoint policy and reacquire credentials without forwarding the previous Authorization header.
Set `UseProxy = false` for the pinned direct-connect profile, or define and validate a single trusted egress-proxy
profile instead—do not leave ambient proxy discovery enabled. Normalize IPv4-mapped IPv6 and all textual IP forms
before prohibited-range checks. Story 13.1 acceptance must prove the policy is used by both Octokit and Forgejo,
including readiness and later mutations.

### TECH-H5 — S-10 names controls that do not authenticate the Dapr sidecar-to-app channel

**Evidence.** S-10 allows Dapr callbacks outside bearer authentication but says they require “mTLS, Dapr app
identity, and component/topic allow-lists” (`architecture.md:703`; reconciliation `:81`). Dapr mTLS protects
sidecar-to-sidecar/control-plane traffic. For HTTP calls from the colocated sidecar to the application—including
`/dapr/subscribe`, pub/sub delivery, bindings, and service invocation—Dapr's documented application-channel
authentication is the `APP_API_TOKEN`/`dapr-api-token` mechanism. The current Server maps `MapSubscribeHandler()`
under ordinary ASP.NET middleware and has no platform-token authentication.

**Impact.** Installing fallback authorization exactly as written either blocks Dapr callbacks or exempts callback
routes without an enforceable caller credential. “Dapr app identity” is not a bearer substitute on this hop.

**Required correction — autofix.** Keep Dapr mTLS and access policies for sidecar-to-sidecar traffic, but require a
per-workload Dapr app API token for sidecar-to-app callback routes, injected with the Kubernetes
`dapr.io/app-token-secret` annotation and validated in constant time by middleware before the callback endpoint.
Bind the application port to the pod/sidecar channel, add NetworkPolicy, and retain component/subscription scopes
and topic allow-lists as authorization controls. The endpoint may bypass *bearer* authorization only after this
platform authentication succeeds. Story 13.2's metadata conformance test must be paired with negative requests
for missing/wrong app token; endpoint metadata enumeration alone cannot prove the custom middleware ran.

### TECH-H6 — Production state-store generation and recovery authority are still open

**Evidence.** D-3/I-2 choose `state.postgresql` but never choose component generation v1 or v2
(`architecture.md:677`, `:802`; deployment profile `:18-21`). The tracked EventStore production component and its
OQ8 fixture use `version: v1` (`references/Hexalith.EventStore/deploy/dapr/statestore-postgresql.yaml:20-29`). Dapr
now recommends v2 for new applications, and v1/v2 cannot read the same tables or migrate through Dapr. Separately,
I-11 introduces a signed WORM deletion/hold ledger used to change restored state but leaves the signer, cursor,
idempotency, EventStore ownership, restore-admission integration, and legal-hold-aware retention undefined
(`architecture.md:811`; backup runbook `:14-40`). The ledger has a fixed 400-day retention even though an active
legal hold can outlive that interval.

**Impact.** Story 13.5/13.7 can choose incompatible PostgreSQL layouts, making the first later component upgrade a
data migration. The recovery workflow also risks creating an out-of-band domain authority in object storage,
contrary to the EventStore-only persistence rule, and can forget a still-active hold after 400 days. Current
EventStore restore admission will return `DeferredValidation`, so the runbook cannot reach traffic admission with
the platform as built.

**Required correction — discuss then encode.** Choose `state.postgresql` v2 now for the greenfield supported
production profile, subject to an EventStore v2 actor/replay/backup compatibility lane; if v1 is deliberately kept
for existing OQ8 evidence, state that explicitly and add an approved future migration boundary. Make the WORM
chain an EventStore-platform-owned recovery-safety export, not a Folders/raw-object-store write. Bind signer key
custody, monotonic export watermark, idempotent replay, integrity verification, and EventStore restored-backup
admission. Retain hold records for the hold's lifetime plus the governed post-release interval, never a bare
400-day maximum. Add the missing platform work to TECH-H1's execution dependencies.

## Medium findings

### TECH-M1 — “No blind retry” is ambiguous against EventStore's current bounded re-evaluation

**Evidence.** D-11 says infrastructure never blindly retries a losing command (`architecture.md:685`). Current
EventStore defaults `MaxPersistenceConflictRetries` to 1 and, after a failed actor-state commit, clears the actor
cache, rehydrates state, invokes the domain service again, and retries the batch
(`CommandConcurrencyOptions.cs:9-15`; `AggregateActor.cs:973-976`, `:1222-1275`). This is not a byte-for-byte blind
append, but it is an automatic infrastructure retry of the same command.

**Required correction — autofix.** Either explicitly permit one bounded *full re-evaluation* after cache clear and
rehydration while forbidding blind append/external-side-effect retry, or pin
`EventStore:CommandConcurrency:MaxPersistenceConflictRetries=0` in preproduction/production and test that setting.
The reconciliation must use the same wording. The former matches current platform behavior and keeps aggregates
pure; the latter matches the most literal reading of D-11.

### TECH-M2 — Two application replicas do not complete the availability topology

**Evidence.** I-10 sets two replicas for four application workloads, but the supported profile does not bind the
availability posture of Dapr placement/scheduler/control-plane services, the Redis Streams broker, ingress, or the
PostgreSQL connection pool/proxy. It says replicas span failure domains but does not require topology-spread,
anti-affinity, disruption budgets, or a broker persistence/replication mode.

**Required correction — defer only to exact Story 13.7 acceptance.** The architecture need not choose vendor SKU
or resource sizes, but Story 13.7 must prove no stated two-replica service depends on a single-instance Dapr control
plane, broker, ingress, or database endpoint and must include topology-spread/PDB evidence. Otherwise the replica
floor is numerically precise but operationally false.

## Low findings

### TECH-L1 — Target labels should be local to D-3/D-11/D-12 and S-9/S-10

The validation section eventually explains that these mechanisms have no implementation evidence, but their
decision rows read in the present tense. Add compact `TARGET — NOT BUILT` markers or a single banner immediately
above each table. This avoids later readers citing a target row while missing the distant planning-consistency
qualification.

## Recommended gate disposition

- **Autofix before handoff:** TECH-H2, H3, H4, H5, M1, and L1.
- **Architecture/Delivery reconciliation required before claiming a satisfiable schedule:** TECH-H1 and H6.
- **May remain Story 13.7 acceptance detail, but must be named there:** TECH-M2.

After those changes, rerun this technology/reality lens against the final architecture and reconciliation. The
underlying stack choices—Dapr actors, PostgreSQL actor state, EventStore-only domain persistence, ASP.NET Core
fallback authorization, pinned `SocketsHttpHandler` connections, and AES-GCM—are fit for the target when the
ownership and channel semantics above are made explicit.

## Re-review after corrections — 2026-09-17

**Verdict: FAIL.** The corrected package resolves the original platform-ownership, token-only PD8, concurrency,
SSRF/proxy/redirect, PostgreSQL-v2, recovery, multi-replica, and bounded-re-evaluation findings. The external
EventStore capabilities are now honest lower-rank prerequisites, and the recovery-safety export remains inside the
EventStore write boundary. This section supersedes the earlier gate verdict and disposition; only the two concrete
blockers below remain.

### High findings

#### TECH-R1 — The normative envelope inventory still contradicts D-12's evolution contract

**Evidence.** D-12 now correctly requires EventStore envelope metadata to carry stable `logicalEventType` and a
positive `eventPayloadSchemaVersion`, distinct from `MetadataVersion`, and limits missing versions to a closed
legacy registry with explicit `EventTypeName` aliases (`architecture.md:698`). The later normative "Command and
event envelope" inventory still says the required metadata ends with `eventTypeName` and does not mention either
new field or mark that name as legacy (`architecture.md:1010-1014`). The downstream amendments require retained
fixtures and a closed missing-version registry, but Story 12.1 does not explicitly require the alias inventory that
D-12 makes necessary (`reconcile-architecture-downstream-2026-09-17.md:83-84`).

**Impact.** `EXT-ES-EVENT-EVOLUTION` and Folders 12.1/12.2 still have two plausible normative contracts. Delivery
can preserve the CLR-derived `EventTypeName`-only envelope and claim conformance to the later inventory, leaving
historical replay dependent on rename-prone CLR identity and the legacy-version exception unimplementable as a
closed registry.

**Required correction.** Reconcile the envelope inventory with D-12: require `logicalEventType` and
`eventPayloadSchemaVersion` for target writes; describe `EventTypeName` only as the explicit alias input for named
pre-adoption records. Add the exact legacy alias inventory/fixture obligation to Story 12.1 (with Story 12.2 replaying
every registered alias from an empty checkpoint). Keep the platform envelope/registry API and fixtures as
`EXT-ES-EVENT-EVOLUTION` acceptance evidence.

#### TECH-R2 — The app-token manifest contract configures the sidecar but not the validating application

**Evidence.** S-10, the supported profile, and Story 13.2 require `dapr.io/app-token-secret` plus constant-time
application middleware (`architecture.md:715`; `docs/deployment/supported-mvp-profile.md:48-52`;
`reconcile-architecture-downstream-2026-09-17.md:89`). That annotation tells the Dapr injector which secret to
expose to the **sidecar**, which then sends `dapr-api-token` on callbacks. Dapr separately requires the application
to receive the same secret so it has an expected value to compare; its Kubernetes guidance mounts the secret into
the application container ([Dapr app API token](https://docs.dapr.io/operations/security/app-api-token/)). None of
the three governing texts requires that application-side secret reference or names the incoming header.

**Impact.** A manifest can satisfy the written annotation/listener requirements while leaving the middleware with
no trusted comparison value. Delivery must either invent the secret wiring or ship an exemption that cannot
authenticate the sidecar-to-app hop.

**Required correction.** Require each callback-hosting workload manifest to reference one per-workload Kubernetes
secret both through `dapr.io/app-token-secret` for the sidecar and through an application-container secret
environment/configuration reference for the validator. Bind the middleware to the incoming `dapr-api-token` header,
forbid token logging, and retain the missing/wrong/correct-token plus public/direct-pod denial tests already named.

### Medium findings

None.

### Low findings

None.

## Final follow-up re-review — 2026-09-17

**Verdict: PASS.**

- TECH-R1 is closed: the normative envelope inventory now requires `logicalEventType` and positive
  `eventPayloadSchemaVersion` for target events, makes `MetadataVersion` distinct, confines `EventTypeName` to the
  closed legacy-alias path, and binds Story 12.1 plus `EXT-ES-EVENT-EVOLUTION` evidence to one retained historical
  byte fixture per alias.
- TECH-R2 is closed: S-10, the supported profile, and Story 13.2 now bind the same per-workload Kubernetes secret to
  the sidecar through `dapr.io/app-token-secret` and to the application through
  `env.valueFrom.secretKeyRef`/`APP_API_TOKEN`; middleware validates the incoming `dapr-api-token` in constant time,
  never logs either value, and tests absent application secret plus missing/wrong/correct headers.

**Remaining concrete technology/reality blockers: none.** Pending human approvals, Delivery regeneration, external
EventStore releases, implementation, and release evidence remain explicitly tracked gates rather than unresolved
technology decisions or inaccurate as-built claims.
