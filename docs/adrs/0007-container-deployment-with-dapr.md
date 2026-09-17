# ADR 0007: Container deployment with Dapr sidecars and stable app IDs

Date: 2026-05-31
Amended: 2026-09-17

Decision identifiers: `I-1`, `I-2`, `I-3`, `I-4`, `I-10`, `I-11`. Implementing stories: Epic 7 stories 7.1 and 7.3 for existing images/app IDs, and reserved Story 13.7 for the supported production topology and recovery evidence.

## Status

Accepted architecture; production-profile implementation and evidence pending Story 13.7. The existing container-per-service model and stable app IDs are retained. The 2026-09-17 amendment supersedes the earlier “Kubernetes-friendly but not required” production ambiguity.

## Context

Hexalith.Folders runs as several cooperating services that communicate through Dapr. Deployment must be portable across environments, secure by default in production, and identical in service identity between local and production so access-control configuration is portable. The local Aspire topology must mirror the production wiring.

All conformance and recovery evidence named by this ADR is metadata-only.

Architecture decisions `I-2`, `I-3`, and `I-4` pin the production hosting model, the Dapr access-control posture, and the stable service identities. Decision `I-1` pins the local Aspire orchestration that mirrors them.

## Decision

Each service ships as one container image with a Dapr sidecar, production Dapr is deny-by-default with mTLS, and service identities are stable across environments.

- `I-2`: the one supported MVP serving profile is single-region Kubernetes with a Dapr sidecar alongside each Folders service, external highly available PostgreSQL through Dapr `state.postgresql` v2 for EventStore, and a separate durable/replicated Redis Streams-compatible broker. The EventStore v2/recovery platform capability is an explicit external prerequisite.
- `I-3`: local Dapr uses a development access-control config; production uses deny-by-default plus mTLS, validated by the `dapr-policy-conformance` negative test suite.
- `I-4`: Dapr app IDs are stable across environments - `eventstore`, `tenants`, `memories`, `folders`, `folders-workers`, and `folders-ui` - so access-control YAML is portable; the AppHost fails fast if the access-control configuration is missing.
- `I-1`: the local `.NET` Aspire topology in `Hexalith.Folders.Aspire` mirrors the production state-store and pub/sub wiring.
- `I-10`: local and CI are non-release; preproduction mirrors production. Application replicas use topology spread/PDBs, and Dapr control plane, ingress, database endpoint/pooler, and broker may not introduce a singleton. Scaling and configuration rules are fixed in `../deployment/supported-mvp-profile.md`.
- `I-11`: RPO ≤5 minutes and RTO ≤4 hours include loss of the serving region. WAL/recovery points, key custody, and the EventStore-owned signed deletion/legal-hold recovery export occupy separately failed recovery boundaries. Restore is isolated in the recovery region, fails closed on export gaps/corruption, reapplies later dispositions, and is drilled before release and quarterly; see `../runbooks/backup-restore.md`.

Preproduction and production manifests keep the same app IDs, component types, policy names, and production config names while selecting environment-owned image references.

## Consequences

- Access-control configuration is portable because app IDs do not change between environments.
- Production is secure by default: a missing or misconfigured policy fails the `dapr-policy-conformance` suite rather than silently allowing traffic.
- Multi-replica availability does not permit concurrent aggregate writers: EventStore `AggregateActor` turn-based execution remains the sole writer per aggregate identity.
- Production now carries PostgreSQL and Kubernetes operational dependencies and must demonstrate backup/restore, scaling, and policy evidence in preproduction.
- The cost is that app IDs and production config names are stable contracts; renaming a service is a coordinated change across images, Dapr config, and the access-control YAML.

## Alternatives Considered

- A single combined container for all services was rejected because it couples scaling and failure domains and breaks the per-service Dapr sidecar model in `I-2`.
- Environment-specific app IDs were rejected by `I-4` because they make the deny-by-default access-control YAML non-portable and error-prone across environments.
- Redis as the authoritative production actor store was rejected because the supported profile requires transactional state plus a named PITR recovery contract.
- Non-Kubernetes production was rejected for MVP because multiple untested topologies would make OQ12/OQ13 evidence non-reproducible.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`. The current gate still inventories seven runbooks and the older ADR 0007 decision-ID set, so passing it does not validate `backup-restore.md` or I-10/I-11. Story 13.7 must extend that inventory and its metadata-only negative controls. `EXT-ES-RECOVERY` must first publish the platform capability, then Story 13.7 adds manifest parity, app-token callback isolation, substrate availability, cross-region PITR/export, isolated regional restore, and RPO/RTO drill evidence before this ADR can be cited as implemented production capability.
