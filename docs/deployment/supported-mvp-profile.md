# Supported MVP Deployment Profile

Status: architecture target; implementation and release evidence pending Story 13.7.

This document is co-normative with architecture decisions I-2, I-10, and I-11. It defines the single profile
against which OQ12 security hardening and OQ13 performance/capacity are evaluated. It does not claim that the
manifests, automation, or evidence exist today.

## Environment inventory

| Environment | Purpose | Release authority |
| --- | --- | --- |
| `local` | Aspire development with Redis-backed local Dapr components | none |
| `ci` | Ephemeral contract, conformance, replay, and policy tests | merge evidence only |
| `preprod` | Same component types, Dapr policies, app IDs, and topology as production | sole release-evidence environment |
| `production` | Supported single-region Kubernetes profile | live service after all gates and approvals |

Preproduction and production use Kubernetes, Dapr mTLS with deny-by-default access control, external highly
available PostgreSQL through Dapr `state.postgresql` **v2** as EventStore's transactional actor state store, and
a separate durable/replicated Redis Streams-compatible pub/sub broker. The tracked v1 component is not production
evidence; `EXT-ES-RECOVERY` must publish v2 actor/replay/backup compatibility first. Projections are rebuildable
and never recovery authority. `local` and `ci` substitutions cannot satisfy production evidence.

## Services and replica floor

| Dapr app ID | Role | Production minimum | Scaling signal |
| --- | --- | ---: | --- |
| `eventstore` | aggregate actors, event persistence/upcasting, confidential token aliases, recovery-safety export | 2 | actor turn queue, command latency, CPU |
| `folders` | v2 REST and domain-service host | 2 | request concurrency, p95 latency, CPU |
| `folders-workers` | workflows, provider calls, reconciliation, cleanup | 2 | queue depth, oldest-message age, worker saturation |
| `folders-ui` | read-only operations console | 2 | request rate, p95 latency, CPU |
| `tenants` | external authority dependency | external SLO | dependency health and authority freshness |
| `memories` | external indexing/search dependency | external SLO | dependency health and egress backlog |

Replicas use topology spread/anti-affinity and disruption budgets across failure domains. Dapr placement,
scheduler/control-plane services, ingress, the PostgreSQL endpoint/pooler, and the broker must also lack a
single-instance dependency; broker persistence/replication is recorded in the manifest. Initial requests,
limits, maxima, and thresholds are calibrated against C1/C5. EventStore actor placement and D-11 preserve one
writer per aggregate regardless of replica count.

## Configuration and security

Configuration precedence is checked-in non-secret defaults, then environment ConfigMap, then external
secret-store reference. A production key with no declared source fails startup. Command-line, pod-local, and
replica-local correctness overrides are forbidden. Stable app IDs are `eventstore`, `tenants`, `memories`,
`folders`, `folders-workers`, and `folders-ui`.

Every external route uses HTTPS. S-10 fallback authorization protects routes by default; only liveness is public.
Dapr callbacks use a separate loopback app-channel listener and no Service/Ingress exposure. Each callback-hosting
workload references one per-workload Kubernetes secret through both `dapr.io/app-token-secret` for the sidecar and
application-container `env.valueFrom.secretKeyRef` as `APP_API_TOKEN`; middleware constant-time compares the
incoming `dapr-api-token` header and never logs token values. Dapr mTLS/access policy and component/topic scopes remain
separate controls. Provider egress follows S-9 with direct connect (`UseProxy=false`), cross-authority credential
redirect denial, address normalization, and both-adapter tests. Network policy matches only declared edges.

## State and recovery

PostgreSQL is the authoritative write side. The RPO ≤5 minutes and RTO ≤4 hours cover loss of the serving region.
Continuous WAL/PITR and encrypted daily recovery points retained 35 days are copied to a separate recovery
region and independent failure/account boundary; dual-control key custody fails separately and recovery-region
capacity/configuration is pre-authorized. EventStore exports a signed metadata-only deletion/legal-hold chain
from committed events within five minutes to encrypted WORM storage in that recovery boundary. Its KMS signer,
monotonic watermark, integrity chain, idempotent replay key, lag alert, and restore-admission validation are
platform-owned. Non-held rows retain 400 days; active holds retain through hold lifetime plus 400 days after
release. Redis Streams and projections are never promoted to authority.

The normative restore and deletion-reconciliation procedure is
[`../runbooks/backup-restore.md`](../runbooks/backup-restore.md). A successful isolated restore drill is required
before the first release and quarterly thereafter.

## Story 13.7 acceptance evidence

- Production and preproduction manifests prove component type/version, app ID, access policy, both app-token secret
  references, incoming-header validation/listener isolation, topology spread/PDB,
  control-plane/ingress/database/broker availability, and configuration parity.
- A multi-replica test proves D-11 single-writer behavior and canonical `concurrency_conflict` handling.
- C1/C5 results bind resource envelopes and autoscaling maxima to this profile.
- PostgreSQL backup automation demonstrates ≤5-minute cross-region recovery-point coverage.
- The EventStore-owned recovery-safety export proves signing-key custody, monotonic watermark/idempotent replay,
  ≤5-minute export lag alert, WORM retention including active holds, loss/corruption fail-closed behavior, and
  restoration of its control state without hand-seeded rows.
- An isolated **regional-loss** drill records start/end time, selected recovery point, stream/ledger integrity,
  separate key recovery, later-disposition replay, projection rebuild, reconciliation, and traffic admission.
- The ADR/runbook docs gate inventories `backup-restore.md` as its eighth runbook, validates its required sections
  and metadata-only negative controls, and recognizes ADR 0007 decision IDs I-10/I-11; the current seven-runbook
  gate is insufficient evidence for this profile.
- A2 admission: Product + Architecture + Security + Operations + Test. A2b inventory: the same roles.
  `OQ12-EVIDENCE`: Product + Architecture + Security + Operations + Test. `OQ13-EVIDENCE`: Product +
  Architecture + Operations + Test. These remain separate approval objects.
