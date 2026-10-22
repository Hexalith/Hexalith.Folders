# Retention And Tenant Deletion

Ordinary push and pull-request CI runs `tests/tools/run-retention-deletion-gates.ps1` as a blocking same-commit policy check. The semantic-release package lane remains independent and does not accept a checked-in retention report as package-sealing evidence.

Story 7.11 provides the existing static retention evidence; Story 4.22 owns the revised staged-content clock and cleanup trigger. This document does not add runtime deletion endpoints, background cleanup workers, provider cleanup automation, or UI mutation.

## Per-class C3 mapping

The authoritative per-class map lives in two cross-referenced artifacts, both validated by `pwsh ./tests/tools/run-retention-deletion-gates.ps1`:

- `docs/exit-criteria/c3-retention.md` (Machine-Validated Policy Source table) maps each required data class to its retention class identifier, retention duration, cleanup trigger, disposal behavior, tenant-deletion disposition, tenant-isolation implication, observability evidence, owner, authority, and approval state.
- `docs/runbooks/tenant-deletion.md` (Disposition matrix) maps each data class to its tenant-deletion disposition (`deleted`, `tombstoned`, `retained`, or `anonymized`), manual-versus-automated step, retention behavior, and metadata-only audit reconstruction.

The required classes are `Audit metadata`, `Workspace status`, `Provider correlation IDs`, `Read-model views`, `Temporary working files`, and `Cleanup records`. The owning implementation surface and future consumer for each class are recorded in the C3 policy table's provenance and future-consumer columns, and the release validation command for every class is `pwsh ./tests/tools/run-retention-deletion-gates.ps1`.

## Local command

Run the same release evidence sequence locally:

```powershell
dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
dotnet build Hexalith.Folders.slnx --no-restore -m:1
pwsh ./tests/tools/run-retention-deletion-gates.ps1
```

The gate writes `_bmad-output/gates/retention-deletion/latest.json`.

## Evidence reviewed for release

Release reviewers inspect:

- `docs/exit-criteria/c3-retention.md`
- `docs/operations/retention-and-tenant-deletion.md`
- `docs/runbooks/tenant-deletion.md`
- `docs/exit-criteria/c0-c13-governance-evidence.yaml`
- `_bmad-output/gates/retention-deletion/latest.json`
- `deploy/nuget/release-packages.yaml`
- `.github/workflows/release.yml`

The latest report must carry the current full source commit, required C3 class coverage, tenant-deletion disposition rows, release evidence paths, bounded validation categories, and `diagnostic_policy: metadata-only`.

## Approval rules

C3 is approved for the temporary-working-files trigger under A7b. The 2026-06 Legal + PM record remains current for unaffected rows; Jerome approved the revised A7b payload for Legal + Product + Security + Architecture on 2026-09-17 at SHA-256 `1bd514dc073c728a290f94de4384534d4e9bd843b208c119f52644d6fe07bed1`. This design approval does not claim that deferred Story 4.22 runtime implementation or evidence is complete.

In short: approved design evidence does not clear live release until the separately governed runtime evidence is complete.

Do not change `AuditTrailQueryHandler.RetentionClassToken` or `OperationTimelineQueryHandler.RetentionClassToken` from explicit reference-pending markers unless C3 contains approved retention class identifiers and all affected contracts, generated clients, fixtures, UI tests, and docs are updated from the authoritative source.

## Failure categories

The retention/deletion gate blocks release evidence for:

- Missing C3, operations, runbook, governance, release workflow, package manifest, or package gate artifacts.
- Missing required C3 class coverage.
- Missing retention duration, cleanup trigger, operational evidence, tenant-deletion disposition, tenant-isolation implication, owner, authority, or review date.
- Missing tenant-deletion behavior for `deleted`, `tombstoned`, `retained`, or `anonymized` records.
- Missing or mismatched A7b Legal + Product + Security + Architecture approval for the revised working-file trigger.
- Stale source commit in checked evidence.
- Unsafe diagnostic text, absolute evidence paths, malformed JSON/YAML/Markdown, or nested submodule setup.

## Tenant-deletion policy

Tenant deletion never authorizes cross-tenant lookup. Counts and records use tenant-scoped synthetic IDs such as `tenant-001`, `operation-001`, and `task-001`. Retained records are metadata-only. Anonymized records remove display aliases while preserving authoritative tenant-scoped IDs required for audit, replay, reconciliation, and legal review.

Tenant deletion must not erase audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations. Commit operation IDs inherit the C3 audit retention duration and remain scoped to the managed tenant.

Temporary working files are disposable cache, not authoritative state, but staged content is never discarded by a recovery timer or lifecycle transition. The non-destructive recovery deadline only gates return to `dirty`. Cleanup starts a separate P7D epoch after terminal task closure with no active task, runs no earlier than `stagedCleanupNotBefore`, rechecks terminal/no-active state, and remains blocked by legal hold. Legitimate resume cancels the epoch; later qualifying closure receives a fresh P7D window. Cleanup records are metadata-only evidence and must not include file contents, provider payload bodies, raw diffs, credentials, environment dumps, production URLs, stack traces, or local absolute paths.

## Rerun and recalibration rule

Rerun the retention/deletion gate whenever C3 durations, approval state, retention class identifiers, audit/timeline retention behavior, cleanup policy, tenant-deletion disposition, release evidence paths, or package-publish policy changes. A release recalibration must update C3, the tenant-deletion runbook, governance evidence, conformance tests, package-gate validation, and the latest report together.

## Submodule policy

Use only root-level submodule initialization:

```text
git submodule update --init --checkout references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Do not initialize nested submodules for this lane.

## Durable operational status expiry and repair

Lifecycle, workspace, lock, cleanup and task metadata expires at its original source event time plus
400 days. Live delivery and rebuild write only the remaining TTL for views, their integrity evidence,
and source-position/message receipts; terminal rewrites do not restart retention. The bounded metadata
checkpoint has no TTL. Clock-derived freshness permits five minutes of reader clock lag without changing
original source timestamps. A view at the inclusive 400-day boundary, or a view whose row and integrity
evidence have both expired, returns the existing safe v1 not-found outcome without retry. Surviving
integrity evidence with a missing row remains canonical unavailable and requires repair. Source-folded
task closure authority remains independent of these rebuildable rows, so expiry cannot reopen a task.

Workers performs ordinary operational delivery. The legacy `/project` command callback does not rebuild
operational status. Use the authorized SDK named `/project/rebuild/shared/v1` transport to bootstrap
history from before enablement or repair a folder stalled by a missing/dead-lettered source delivery.
That transport owns Begin, Abort, Verify and sealed-source catch-up; abort failed private generations
through the same SDK session rather than leaving a tenant rebuilding. Until repair completes, the folder
honestly reports `projection_stale`. This operator repair guidance adds no public UI repair action,
audit erasure, destructive cleanup or new retention authority. Cleanup eligibility/status details beyond
the fail-safe `status_only` view remain with Stories 12.3–12.7 and 4.22.

The approved EventStore source-expiry capability records the absolute deadline in each retained
operation and shared mutation. Folders supplies that deadline instead of a relative TTL. SDK staging,
compaction and recovery preserve it; values already past the deadline are omitted or conditionally
removed, and the non-expiring checkpoint is retained. Versioned deadline metadata also bounds reads
through provider TTL rounding. Existing relative-TTL manifests retain their original semantics; this
capability does not invent source deadlines for old manifests that never recorded one.

The SDK also recovers retained private scratch batches before retrying or advancing an epoch phase,
and reconciles private keys before named-session cleanup. Use the same named action/Abort transport.
Legacy prepared scratch can be compensated only when the original matching envelopes and unchanged
fence prove its bounded shape; committed or insufficient legacy evidence stays unavailable for owner
resolution. Do not erase SDK metadata or force the epoch open. This local source qualification does not
authorize a deployment, package publication or dependency pin change; the supported Redis profile
remains resumable. Consumer qualification is recorded in the Story 12.2 evidence.


## Authoritative workspace bodies — owner decision 2026-10-22

Jerome approved the authoritative-body prerequisite and disposition in
`_bmad-output/implementation-artifacts/story-12-3-content-prerequisite-proposal-2026-10-22.md`
with "approve both and continue". This class is separate from disposable temporary working files.

EventStore owns the physical body namespace, immutable tenant/task receipts, atomic publication ledger,
legal holds, abandoned-upload dispositions and cleanup epochs. Folders events, snapshots, projections,
audit and diagnostics remain metadata-only. Working copies and indexes never establish body truth.

- Preserve every live staged/current-committed reference without automatic TTL. Pending/indeterminate
  publication remains protected; lock loss, restart, recovery expiry and archive alone authorize no deletion.
- Accepted removal suppresses ordinary retrieval immediately, including all versions of that incarnation.
  Legal hold preserves physical bytes without restoring visibility. Replay cannot resurrect a tombstone.
- Superseded, removed and abandoned bodies qualify only with no live authoritative reference, terminal
  associated work and no active task. A never-assigned upload first requires its durable abandoned disposition.
- Begin a fresh seven-calendar-day (`P7D`) epoch at qualification. Legitimate resume cancels it; later
  eligibility starts a new epoch. A hold blocks erasure; release requires a fresh qualifying epoch.
- At deletion, recheck source generation, terminal/no-active/reference predicates and SDK-owned hold
  generation. Acquire a revocable unique CAS claim against references/holds, then serialize an explicit
  metadata-only receipt-disposal grant through the owning EventStore aggregate. The source grant is the
  irreversible decision forbidding every later reference to that receipt; unrelated later lock/task events
  remain allowed. A resumed task before this grant invalidates its source generation. Recovery before the
  SDK commits deletion re-reads disposition and re-proves the source grant; a changed generation requires
  a fresh P7D epoch. The SDK commits `DeletionAuthorized` only while it still owns the exact claim, before
  deleting any bytes. No concurrent visitor may clear this durable phase or admit holds/references afterward.
  Replacement processes may finish physical deletion from that decision, including after deletion succeeded
  but the completion marker did not persist. Unavailable authority or unresolved publication before the
  irreversible decision preserves bytes. Retain independently governed receipt/tombstone metadata.

The content administrative transport requires current `manage_folder_access` authority for its exact tenant,
folder and workspace; it delegates hold/release/abandon/apply to the SDK. It does not expose v2 or execute Git.
Source qualification and live Folders acceptance evidence are recorded with Story 12.3; this policy record
alone claims no runtime completion or deployment.
