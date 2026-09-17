# Backup and Restore Runbook

Status: architecture target; automation and first drill pending Story 13.7.

This runbook defines recovery for the supported MVP deployment profile. It is metadata-only: evidence must use
synthetic identifiers and must never contain credentials, plaintext confidential values, production endpoints,
database dumps, keys, provider payloads, or tenant content.

## Purpose

Restore the authoritative EventStore after a serving-region failure without resurrecting deleted data, losing
legal holds, retrying ambiguous provider effects, or promoting a projection or broker to domain authority.

## Objectives and authority

- Authoritative EventStore PostgreSQL: RPO ≤5 minutes; RTO ≤4 hours, including total loss of the serving region.
- Continuous WAL/PITR plus encrypted daily recovery points retained 35 days in a separate recovery region and
  independent failure/account boundary; recovery-region capacity and configuration are pre-authorized.
- Backup encryption keys are recovered from separately failed custody under dual control.
- EventStore exports a signed metadata-only deletion/legal-hold recovery-safety chain within five minutes to
  encrypted WORM storage in that recovery boundary. Non-held entries retain 400 days; active holds retain for
  the hold lifetime plus 400 days after release. The KMS signer, monotonic watermark, idempotent replay key,
  integrity chain, export-lag alert, and restored-backup admission are platform-owned.
- Event streams and durable command state are authoritative. Projections rebuild; broker state is reconciled.
- Restore never bypasses C3 deletion, tenant-deletion, legal-hold, or cryptographic-erasure obligations.

## Preconditions

1. Declare the incident, recovery owner, UTC recovery window, selected recovery point, and isolated target
   namespace using metadata-only identifiers.
2. Freeze production writes and provider side effects. Do not retry unknown provider outcomes.
3. Confirm authorized Operations and Security participants and dual-control access to backup keys.
4. Capture the current EventStore export watermark and integrity-chain head from WORM control storage; do not
   write domain dispositions directly to object storage. Verify the separate signer and that export lag is
   within five minutes. The chain contains only tenant partition, opaque subject ID, disposition class,
   effective time, hold state, idempotent replay key, and integrity links.

## Procedure

1. Provision an isolated namespace in the recovery region with no public ingress and provider egress disabled.
   Deploy the same image digests, stable Dapr app IDs, PostgreSQL major version, and Dapr `state.postgresql` v2
   component configuration as the affected release.
2. Restore the newest recovery point satisfying incident and integrity criteria, then replay WAL to the selected
   point. Recover encryption keys through the separate dual-control path.
3. Validate PostgreSQL recovery completion, EventStore stream/snapshot hashes, D-12 schema/upcaster coverage,
   command/idempotency state, and S-6 token-alias integrity. A missing version/alias or broken chain stops restore.
4. Through EventStore restored-backup admission, verify the export chain and idempotently apply every deletion,
   tombstone, cryptographic-erasure, and legal-hold disposition later than the recovery point. A watermark gap,
   signature failure, duplicate conflict, expired active hold, or corruption keeps the namespace isolated.
5. Rebuild disposable projections from EventStore. Reconcile pending commands, unknown provider outcomes, and
   broker work from durable state without blind external retries.
6. Run authorization, tenant-isolation, confidential-value sentinel, lifecycle replay, readiness, and sampled
   business-flow checks. Keep provider mutations disabled; use read-only evidence checks for ambiguous outcomes.
7. Compare the measured recovery point and elapsed time with the RPO/RTO objectives. Any miss blocks admission
   and escalates to Operations + Architecture + Security + Test.
8. Admit traffic only after the recovery owner and Security reviewer sign the metadata-only validation record.
   Re-enable provider effects last, after reconciliation queues are bounded and authority evidence is fresh.

## Verification

Run a regional-loss drill once before first production release and at least quarterly. Also run after a PostgreSQL major upgrade,
backup-provider change, key-recovery change, retention/deletion semantic change, or a failed real recovery.

The evidence record contains only: drill ID, serving/recovery failure-domain identifiers, image/config digests,
recovery-point age, export lag/watermark, start/end UTC timestamps, measured RPO/RTO, stream/chain integrity,
key-version counts, projection/reconciliation outcomes, approver roles, and sanitized failure categories. Story
13.7 must prove the producer, alert, retention, control-state recovery, and regional drill rather than replaying a
hand-seeded ledger.

## Escalation and handoff

- Missing/corrupt recovery point: Operations incident; do not fall back to projections as authority.
- Missing backup/signing key or token-alias integrity failure: Security incident; stop recovery.
- Deletion-ledger mismatch or apparent resurrection: Legal + Security incident; keep namespace isolated.
- Unknown provider outcome: follow [`./reconciliation.md`](./reconciliation.md); never retry blindly.
- RPO/RTO miss: Operations + Architecture + Security + Test must reject admission or approve a new architecture
  decision; an operator cannot waive the objective in this runbook.

## Related evidence

- [`../deployment/supported-mvp-profile.md`](../deployment/supported-mvp-profile.md)
- [`../exit-criteria/c3-retention.md`](../exit-criteria/c3-retention.md)
- [`./tenant-deletion.md`](./tenant-deletion.md)
- [`./rollback.md`](./rollback.md)
- [`./reconciliation.md`](./reconciliation.md)

## Forbidden evidence

Restore and drill records remain metadata-only. They must not contain credentials, key material, plaintext or
reversibly encrypted confidential values, database dumps, raw events, tenant content, provider payload bodies,
production endpoints, environment dumps, stack traces, or host-absolute paths.
