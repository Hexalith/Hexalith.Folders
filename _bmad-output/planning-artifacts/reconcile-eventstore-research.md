# Input Reconciliation — EventStore Domain Aggregate Research

## Input

- Source: `research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md`
- Compared with: `prd.md` (updated 2026-07-14)
- Addendum: none present
- Reconciliation mode: product requirements and product-visible quality outcomes were extracted; implementation mechanics were classified separately rather than promoted into the PRD.

## Overall Alignment

The updated PRD preserves the research's central product conclusions: tenant-scoped folder-domain ownership; repository-backed folders as the initial product boundary; file content outside EventStore; metadata-only events and audit; provider credentials represented only by non-secret references; asynchronous provider work with inspectable outcomes; projections for normal reads; pure separation between product semantics and EventStore/platform mechanics; and no premature independent repository lifecycle in the MVP.

The PRD is materially more specific than the research on public contract authority, cross-surface parity, authorization freshness, safe denial, locking identity, provider-confirmed commit completion, unknown provider outcomes, context-query bounds, retention, and incident-mode visibility. No contradiction was found in those areas.

## Meaningful Gaps and Decisions to Close

### 1. Organization-level repository policy configuration is described but not fully requirement-backed

The research assigns folder-domain organization configuration responsibility for provider bindings, credential references, repository defaults, provider capabilities, and organization-level policy. The PRD's UJ2 and Endpoint Specifications likewise say Ravi configures repository naming policy, default branch policy, and minimum provider capabilities before readiness validation. However, the Functional Requirements do not clearly grant a capability to configure those organization-level policies:

- FR15 covers provider bindings and credential references.
- FR16 validates readiness.
- FR20 lets an authorized actor define or select the branch/ref policy used by repository-backed tasks.
- No FR explicitly covers configuring the tenant/organization repository naming policy, minimum provider capability policy, or default policy inherited by newly created folders.

This is a traceability gap, not an aggregate-design gap. Either add an explicit organization-policy configuration requirement (including who may change it and how changes affect existing folders/tasks) or state that these policies are externally managed inputs and outside the current product contract.

### 2. Folder lifecycle scope is narrower than the research but the omissions are not declared

The research's candidate lifecycle includes rename, move, archive, restore, and eventual termination/tombstone behavior. The PRD requires create, inspect, and archive, and its retention language refers to an "approved deletion workflow," but it does not define rename, move, restore/unarchive, or deletion behavior and does not list them among the explicit MVP non-goals.

The narrower MVP is reasonable, especially because a logical folder is not a filesystem path, but the contract should make the scope intentional. Declare rename, move/reparent, restore/unarchive, and deletion as post-MVP/non-goals, or add the subset needed for current tenant-administration lifecycle. Also clarify whether an archived folder is permanently terminal in v1 or merely non-mutable until a future restore capability exists.

### 3. Projection correctness under duplicate delivery lacks an explicit quality gate

The research calls out at-least-once event delivery and requires projections to tolerate duplicate delivery without drifting. The PRD requires deterministic rebuild from the same ordered event stream and requires asynchronous provider side effects to be safe under duplicate delivery, but it does not explicitly require duplicate event deliveries to leave status, audit, timeline, readiness, and indexing projections unchanged.

Add a product-visible reliability requirement or release gate stating that repeated delivery of an already-applied event cannot duplicate or alter user-visible read-model results. Sequence tracking and the exact idempotency mechanism remain architecture decisions.

### 4. Long-retention event compatibility is stated but not explicitly verified

The research emphasizes replay tests and conservative evolution of persisted event types. The PRD says event payload evolution must use schema versions and backward-compatible consumers, and C3 retains audit metadata for seven years, but Verification Expectations and Contract and Quality Gates do not explicitly require replaying historical event versions through current aggregate/projection code.

Add a compatibility gate using representative prior-version event fixtures to prove that retained streams still replay and rebuild current status, audit, and timeline views after schema evolution. The implementation rule about retaining or upcasting old event types belongs in architecture, but the rebuild outcome belongs in the PRD because it protects long-lived audit and recovery promises.

## Research Content Correctly Kept Downstream

The following research recommendations are architecture, solution-design, implementation, or operations mechanics rather than missing PRD requirements:

- Exact aggregate classes (`OrganizationAggregate`, `FolderAggregate`) and the decision threshold for introducing `RepositoryAggregate`.
- `AddEventStore()` / `UseEventStore()`, `/process` and `/project`, package/project layout, domain-name attributes, and DAPR application routing.
- Actor IDs, state-store keys, topic names, snapshot intervals, state-store selection, config-store registrations, and domain-service version routing.
- Process-manager/worker implementation for provisioning, workspace preparation, file/Git side effects, repair, and webhook translation.
- CloudEvents envelopes, DAPR service invocation/pub-sub, broker selection, retries/circuit breakers, and sequence-tracking implementation.
- Dead-letter drain procedures, snapshot corruption/fallback mechanics, sidecar resource sizing, deployment topology, and cost telemetry. These should be covered by architecture and operational runbooks while satisfying the PRD's inspectability, recovery, freshness, and security outcomes.
- Event and command class names, `Handle`/`Apply` implementation shape, test library choice, and C# project/package structure.

The research's webhook recommendation does not conflict with the PRD: incoming provider webhooks are explicitly excluded from MVP, so webhook translation remains a future integration design concern.

## Reconciliation Result

Four follow-ups merit PRD-level action: close the organization-policy configuration traceability gap; explicitly declare the omitted folder lifecycle operations; require projection idempotency under duplicate delivery; and add historical event replay compatibility evidence. The remaining research content is either already represented by stronger product requirements or correctly delegated to architecture, implementation, and operations artifacts.
