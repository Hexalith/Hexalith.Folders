# C7 Lock And Authorization Timing

status: approved
decision owner: Architecture
approval authority: Architecture + Security
decision version: 1.0.0
approved on: 2026-09-12
approved by: Administrator (Architecture); Administrator (Security)
lock renewal interval seconds: 30
authorization revalidation interval seconds: 15
revocation effect SLO seconds: 60
expired to stale threshold seconds: 60
source inputs: PRD OQ1, Architecture C7, Workspace State and Concurrency, NFR7, NFR21
last reviewed: 2026-09-12
open questions: none for the governed timing decision; runtime evidence remains reference-pending

## Decision

C7 uses the following security-first global profile. Every value is a maximum bound: a tenant override may
only lower it to a positive whole number of seconds. An override that is zero, negative, greater than its
global value, or missing one of the four values is invalid and must not become active.

| Parameter | Approved value | Unit | Boundary semantics | Tenant override limit | Provenance | Approval state |
| --- | ---: | --- | --- | --- | --- | --- |
| Lock renewal interval | 30 | seconds | For the initial lease, `renewalAnchorAt = effectiveAt = acquiredAt`. After each successful renewal, `renewalAnchorAt` becomes that renewal's `effectiveAt`. Renewal is due at `now >= renewalAnchorAt + effectiveRenewalIntervalSeconds`, where the effective interval is the valid tenant override or otherwise 30 seconds. Expiry takes precedence when the current lease ends before that instant. | Positive whole seconds no greater than 30. | PRD OQ1 and Architecture C7 | approved (Architecture + Security, Administrator, 2026-09-12) |
| Authorization revalidation interval | 15 | seconds | Held-lock authority is due for revalidation at `now >= lastSuccessfulAuthorizationValidationAt + 15 seconds`; every renewal and every mutation also requires fresh authorization even when this periodic boundary has not elapsed. | Positive whole seconds no greater than 15. | PRD OQ1, NFR7, and Architecture concern 16 | approved (Architecture + Security, Administrator, 2026-09-12) |
| Revocation-effect SLO | 60 | seconds | No later than `revocationEffectiveAt + 60 seconds`, a held lock is `revoked`/`inaccessible` and later protected work is denied before any side effect. `revocationEffectiveAt` is the authoritative upstream authority source's revocation-effective timestamp, expressed in the shared UTC clock domain used for `now`, `acquiredAt`, `effectiveAt`, and `expiresAt`; local receipt or observation time is not a substitute. Earlier fail-closed denial is valid. | Positive whole seconds no greater than 60. | PRD OQ1, NFR7, and Architecture concern 16 | approved (Architecture + Security, Administrator, 2026-09-12) |
| Expired-to-stale threshold | 60 | seconds | At `now >= expiresAt` the lock is `expired`. It remains expired while `now < expiresAt + 60 seconds` and becomes `stale` at `now >= expiresAt + 60 seconds`. | Positive whole seconds no greater than 60. | PRD OQ1 Workspace State and Concurrency | approved (Architecture + Security, Administrator, 2026-09-12) |

The authorization-revalidation interval must not exceed the revocation-effect SLO. Stale, unavailable,
unknown, or revoked authority fails closed: renewal and protected work are denied, the denial is recorded as
bounded metadata-only evidence, and no file, repository, provider, commit, or protected audit side effect is
started. Only the task that owns the lock may renew it, and each renewal attempt requires fresh
authorization.

A caller-requested lease shorter than the effective renewal interval keeps its requested expiry. Under the
global 30-second interval, for example, a 20-second lease expires at its 20-second boundary before the first
renewal is due. Under a tenant override of 10 seconds, the comparison uses 10 seconds instead: a 5-second
lease expires before renewal, while a 20-second lease is eligible for renewal at the 10-second boundary.
Neither profile rounds a requested lease up, silently extends it, or renews it after expiry. These timings do
not permit another task to take over a lock and do not release staged work automatically.

## Rationale

Fifteen-second authorization revalidation gives the lock owner a short fail-closed authority window, while
the 60-second revocation-effect SLO leaves a bounded end-to-end budget for authority propagation and lock
state observation. A 30-second renewal interval is frequent enough to maintain an active owner lease without
making a renewal a substitute for authorization. The separate 60-second expired-to-stale threshold preserves
the product distinction between an exact lease lapse and the later operator signal.

Treating every value as a tenant ceiling preserves the approved security posture while allowing stricter
tenants. Comparing a requested lease with the effective tenant renewal interval, while keeping expiry
precedence, prevents a short caller lease from being silently strengthened or prolonged by either schedule.

## Verification impact

Offline governance verification must pin the four values, their positive whole-second units and timing
relationships, the initial acquisition/effective renewal anchor, the authoritative shared-UTC revocation
timestamp, effective-interval short-lease behavior, decision version `1.0.0`, artifact SHA-256 digest, and
exactly one Architecture and one Security approval by Administrator dated 2026-09-12. Missing or mismatched
values, version, digest, authority, signer, or date invalidate C7 approval.

Diagnostics are metadata-only. They may identify C7, a bounded rule category, the evidence version, and a
safe hash; they must not expose tenant data, lock ownership proofs, credentials, paths, file content,
provider payloads, diffs, or host-absolute paths.

## Governance and reopen rule

The canonical artifact is this file. `docs/exit-criteria/c0-c13-governance-evidence.yaml` binds version
`1.0.0` and the SHA-256 digest of this file to both approval records. Any artifact-content change, timing-value
change, version change, or digest mismatch reopens OQ1 and returns C7 to an invalid approval state until
Architecture and Security each record a fresh named, dated approval for the new version and digest. Tenant
overrides do not change this canonical artifact and may only tighten its four ceilings.

## Deferred implementation

This decision does not implement a renewal endpoint, scheduler, authorization propagation, revocation
handler, lock takeover, staged-work release, or C6 transition. NFR7 and NFR21 remain reference-pending until
production-path tests prove renewal, revalidation, fail-closed authority loss, revocation within the SLO, and
the exact expiry/stale boundaries.
