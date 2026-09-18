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

C3 is `superseded-pending-reapproval` for the temporary-working-files trigger. The 2026-06 Legal + PM record remains current for unaffected rows and historical for that superseded trigger. A7b requires Legal + Product + Security + Architecture approval of the final digest; until then live release is blocked. The governance vocabulary, gate, tests, release package validation, and latest evidence must be updated by Delivery to represent this state rather than falsely reporting `policy_status: approved`.

In short: pending approval blocks live release while still allowing local static validation to produce bounded evidence.

Do not change `AuditTrailQueryHandler.RetentionClassToken` or `OperationTimelineQueryHandler.RetentionClassToken` from explicit reference-pending markers unless C3 contains approved retention class identifiers and all affected contracts, generated clients, fixtures, UI tests, and docs are updated from the authoritative source.

## Failure categories

The retention/deletion gate blocks release evidence for:

- Missing C3, operations, runbook, governance, release workflow, package manifest, or package gate artifacts.
- Missing required C3 class coverage.
- Missing retention duration, cleanup trigger, operational evidence, tenant-deletion disposition, tenant-isolation implication, owner, authority, or review date.
- Missing tenant-deletion behavior for `deleted`, `tombstoned`, `retained`, or `anonymized` records.
- Pending A7b Legal + Product + Security + Architecture approval for the revised working-file trigger.
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
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Do not initialize nested submodules for this lane.
