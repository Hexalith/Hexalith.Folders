# Folders architecture validation

20 November 2026 · Hexalith.Folders · Whole-record review

**Verdict — changes required.** The record gives implementers conflicting instructions for provider retries, authorization, admission, routing and event delivery. Following the wrong instruction can repeat a provider write or prevent Workers from receiving valid events. Five independent reviews yielded **7 high** and **10 medium** findings after consolidation. The architecture itself was preserved.

## What passed

| Check | Result |
| --- | --- |
| BMAD mechanical lint | Zero findings on the reviewed bytes. The classic decision-table format also received an explicit ID comparison. |
| Decision IDs and file references | All 66 decision-row identifiers matched the parent commit, including occurrence counts. All 29 distinct local file targets in Markdown links existed. |
| Focused .NET conformance | 20 of 20 passed. Two cases checked ADR decision references and the 38 lifecycle diagram edges, one checked C6 vocabulary, and 17 checked console wireflow notes. |
| Delivery and release posture | The record correctly distinguishes actor-backed storage and the five operational readers from outstanding provider, indexing, audit and readiness evidence. Signed 2.0.0 packages and the recorded `V1Only` control have separate qualification gates. |

These checks cover structure and selected conformance properties. The semantic findings below come from comparing the full record with operation schemas, policies, source registrations and deployment declarations. Production deployment, live-provider behavior, failover and full Release CI were outside this validation.

## High findings

### Provider writes after a timeout

The failure taxonomy includes timeout under known failures, where the caller can retry. The recovery rule also classifies a lost response as an unknown outcome. A provider can accept the first write before the response is lost. Tie classification to dispatch and evidence of effect. After ambiguous dispatch, preserve the operation identity, enter `unknown_provider_outcome`, and use the bounded read-only reconciliation procedure before any further write.

[Architecture L114, 1021–1023](../../architecture.md) · [Provider catalog, mutation failure scope](../../../../docs/contract/provider-compatibility-catalog.md)

### Authority for live file reads

The process recipe says “folder ACL check” before file queries. A reader could reasonably treat a read grant as sufficient to return file content. The v2 matrix requires fresh tenant authority and a **folder write grant** for the five live context operations, with path, sensitivity and C4 checks. Metadata-only index search uses a **folder read grant**, security trimming and authoritative hydration. Name both families and their gates in the recipe.

[Architecture L167–169, 1009–1017](../../architecture.md) · [Authorization matrix L125–129](../../../../docs/contract/authorization-matrix.md) · [FR34–FR35 and FR58](../../prd.md)

### Whether search traffic traverses the sidecar

The search guidance calls the production Dapr policy and mTLS unconditional while also documenting an absolute Memories address override. The Server selects the configured absolute address ahead of the sidecar URL, with no environment restriction in that registration. Scope the guarantee to the supported sidecar configuration. Require deployed-profile evidence of the resolved route. The source establishes a configuration-dependent path, not an observation about a live deployment.

[Architecture L167](../../architecture.md) · [Server registration L175–186](../../../../src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs) · [Production Memories access policy](../../../../deploy/dapr/production/accesscontrol.yaml)

### Which side decides idempotency

The process section calls the generated helpers authoritative. The SDK can mirror payload fields, but it cannot supply trusted tenant, credential or task scope. Make the boundary explicit for both REST and Gateway. The caller supplies the opaque key, the authorized server adapter derives the canonical descriptor, and EventStore serializes admission and decides replay or conflict.

[Architecture L666–667, 966–974, 1065](../../architecture.md) · [OQ8 field and authority rules L9–24](../../../../docs/contract/idempotency-and-parity-rules.md)

### Where REST, SDK and McpCli requests go

The canonical diagram points direct REST and SDK clients at a single EventStore command route. The search section also sends every CLI/MCP read through the REST SDK. Show the two supported paths. The generated SDK calls operation-specific Folders REST routes according to routing mode. Eligible McpCli operations target the EventStore Gateway and share the Folders domain checks. Identify legacy SDK wrappers separately. Eligibility and package publication each have a separate runtime qualification gate.

[Architecture L72–73, 167, 1632, 1648–1654](../../architecture.md) · [Generated REST client](../../../../src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs) · [Q33 and Q34 declarations](../../../../docs/contract/mcpcli-folders-operation-declarations.md) · [V2 operation routes](../../../../src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml)

### Which topics Workers consume

The convention and diagram use tenant-prefixed topic names. The Folders publisher override and the Workers subscription bind `folders.events` and `tenants.events`. A publisher following the diagram can send a valid event to a topic the configured subscriber never reads. Show the Folders override in the diagram and identify tenant isolation in the envelope, authority checks and storage keys.

[Architecture L82, 923–924, 1634–1635, 1659](../../architecture.md) · [AppHost publisher L81–87](../../../../src/Hexalith.Folders.AppHost/Program.cs) · [Workers subscription L17–21](../../../../deploy/dapr/production/pubsub.workers.yaml)

### Lock-conflict response on the wire

The A-8 example tells client authors to handle HTTP 409, an uppercase code and `wait_and_retry`. The v2 workspace-lock example specifies HTTP **423**, category `lock_conflict`, code `workspace_locked`, action `retry`, and `details.visibility`. The architecture example also supplies different detail fields. Reference the operation’s generated example so status handling, redaction and retry behavior agree.

[Architecture L897–914](../../architecture.md) · [V2 lock-conflict response L6743–6754](../../../../src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml)

## Medium findings

### Confidential metadata classification

Concern 17 offers hash, truncation, redaction and exposure as general options. S-6 and approved C9 require tenant-scoped, non-reversible tokens at the event-write boundary for confidential overrides, with withheld rendering. Limit the display options to the classes that permit them and refer writers to S-6.

[Architecture L126, 627, 633](../../architecture.md) · [Approved C9 policy](../../authority-relock/2026-09-17/A5-C9-PD8.md)

### Lock prerequisite

“Every mutating call” includes creation and lock acquisition, which cannot start with an existing held lock. Bind fencing to the workspace operations that require it and retain each operation’s distinct admission and authorization prerequisites.

[Architecture L980](../../architecture.md) · [Canonical operation inventory](../../../../docs/contract/authorization-matrix.md)

### Upload media type

The header table specifies multipart streaming. The published AddFile and ChangeFile operations advertise JSON bodies with discriminated payload shapes, including the stream descriptor. Document the advertised wire shape separately from how bytes are supplied and verified. Treat a new media type as a contract change.

[Architecture L895](../../architecture.md) · [V2 AddFile and ChangeFile request bodies](../../../../src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml) · [OQ2 file policy](../../../../docs/contract/file-context-contract-groups.md)

### Correlation and task identifiers

The header table requires a ULID and a correlation header on every request. V2 accepts an optional opaque correlation ID. Task IDs are also opaque, with requiredness determined by operation. Keep local generation of a ULID as a default and accept valid caller-supplied opaque values.

[Architecture L681, 890–891](../../architecture.md) · [V2 header components L16421–16434](../../../../src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml)

### Production broker selection

D-5 permits switching broker families as an escalation. I-10 and the supported deployment profile qualify a durable Redis Streams-compatible broker. Treat another broker as a change to the supported profile with explicit qualification.

[Architecture L607, 736, 744](../../architecture.md) · [Production environment inventory](../../../../docs/deployment/supported-mvp-profile.md)

### Snapshot interval

D-6 calls 50 events the EventStore default. The checked-out SDK sets its fallback interval to 100 and the reviewed Folders host configuration contains no 50-event override. Cite the inherited default or identify an explicit Folders override and its scope.

[Architecture L608](../../architecture.md) · [SnapshotOptions L27](../../../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs)

### EventStore container image

The deployment inventory names `hexalith-folders-eventstore`. The project sets `ContainerRepository` to `eventstore`, which the component-boundary section also records. Point deployment guidance to the project and separately bind the release digest when available.

[Architecture L1563, 1718](../../architecture.md) · [Container project L6](../../../../src/Hexalith.Folders.EventStore/Hexalith.Folders.EventStore.csproj)

### Who approves a new exception

Some current exception clauses still ask for separate role approvals. The September 24 policy makes Jerome the decision owner. Keep security, legal and other specialist evidence attached to the decision and identify dated historical signatories where applicable.

[Architecture L633, 653](../../architecture.md) · [Project decision policy](../../../../docs/governance/approval-policy.md)

### Release evidence assignments

The band-level summary obscures shared story ownership and attaches OQ13 to the NFR band. Use the traceability rows for individual owners, including 7.16 for NFR83. OQ11 covers the durable slice for NFR79–81, OQ12 covers NFR74–78 and 82–84, and OQ13 measures the approved capacity and latency envelope on the same profile.

[Architecture L230–232, 1609, 1751](../../architecture.md) · [NFR74–84 traceability rows](../../../../docs/exit-criteria/nfr-traceability.md) · [PRD open release items](../../prd.md)

### Execution order and approved targets

Phase prose presents earlier C1–C5 decisions as pending and provides an alternative implementation order. Mark the phase sequence as historical. Use the execution manifest for current dependencies and the governance inventory for approved values and remaining measurements.

[Architecture L784–806, 1766–1768](../../architecture.md) · [Execution manifest](../../planning-story-manifest.yaml) · [Exit-criteria inventory](../../../../docs/exit-criteria/c0-c13-governance-evidence.yaml)

## Order of repair

1. Align the provider failure and admission rules with the provider catalog and A-9. A lost response after dispatch must have one recovery path across REST and Gateway.
2. Align authorization, search egress and client routing with the operation matrix, host registrations and deployment profile. Walk one live-context read and one indexed search through each eligible transport.
3. Reconcile topics, wire examples and operational tables against the checked-in artifacts. Review lock-conflict, upload, identifier and event-delivery examples at the boundaries. Rerun the standalone review and the focused checks on the amended record.

## Review record

Reviewed the working copy of `_bmad-output/planning-artifacts/architecture.md` at parent commit `b4c6b9a4`. The five direct submodule checkouts ahead of the parent pins were Builds, FrontComposer, Memories, Projects and Tenants. The scope includes their checked-out source when cited. The evidence is local to this revision and configuration.

Document SHA-256: `9b3ebe4eba08e8c1e1578982b9ed792ca3a213ca33fd613f02749f6fcccf721a`. Architecture line numbers refer to that digest. Findings shared by reviewers are counted once. Details and additional locations appear in the five reviews.

[Good-spine rubric](reviews/review-rubric-2026-11-20.md) · [Technology and runtime](reviews/review-technology-2026-11-20.md) · [Adversarial divergence](reviews/review-adversarial-2026-11-20.md) · [Decision authority](reviews/review-authority-2026-11-20.md) · [Security and tenant isolation](reviews/review-security-2026-11-20.md)
