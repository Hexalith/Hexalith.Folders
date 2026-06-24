# Retention And Tenant Deletion

Story 7.11 turns C3 retention and tenant-deletion policy into static release evidence. It does not add runtime deletion endpoints, background cleanup workers, provider cleanup automation, or UI mutation.

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
- `.github/workflows/release-packages.yml`

The latest report must carry the current full source commit, required C3 class coverage, tenant-deletion disposition rows, release evidence paths, bounded validation categories, and `diagnostic_policy: metadata-only`.

## Approval rules

C3 is `approved`: this artifact now contains explicit Legal + PM approval evidence (PM Jerome 2026-06-22; Legal Jérôme Piquot 2026-06-24, Louveciennes). `pwsh ./tests/tools/run-retention-deletion-gates.ps1` completes local static validation and now reports `status: passed` / `policy_status: approved`; before approval it reported `status: release-blocked` and live package publishing was required to fail before any package push. The C3 doc, governance evidence, the gate, release package validation, tests, and latest evidence were updated together in one commit.

In short: pending approval blocks live release while still allowing local static validation to produce bounded evidence.

Do not change `AuditTrailQueryHandler.RetentionClassToken` or `OperationTimelineQueryHandler.RetentionClassToken` from explicit reference-pending markers unless C3 contains approved retention class identifiers and all affected contracts, generated clients, fixtures, UI tests, and docs are updated from the authoritative source.

## Failure categories

The retention/deletion gate blocks release evidence for:

- Missing C3, operations, runbook, governance, release workflow, package manifest, or package gate artifacts.
- Missing required C3 class coverage.
- Missing retention duration, cleanup trigger, operational evidence, tenant-deletion disposition, tenant-isolation implication, owner, authority, or review date.
- Missing tenant-deletion behavior for `deleted`, `tombstoned`, `retained`, or `anonymized` records.
- Pending Legal + PM approval for live release.
- Stale source commit in checked evidence.
- Unsafe diagnostic text, absolute evidence paths, malformed JSON/YAML/Markdown, or nested submodule setup.

## Tenant-deletion policy

Tenant deletion never authorizes cross-tenant lookup. Counts and records use tenant-scoped synthetic IDs such as `tenant-001`, `operation-001`, and `task-001`. Retained records are metadata-only. Anonymized records remove display aliases while preserving authoritative tenant-scoped IDs required for audit, replay, reconciliation, and legal review.

Tenant deletion must not erase audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations. Commit operation IDs inherit the C3 audit retention duration and remain scoped to the managed tenant.

Temporary working files are disposable cache, not authoritative state. Cleanup records are metadata-only evidence and must not include file contents, provider payload bodies, raw diffs, credentials, environment dumps, production URLs, stack traces, or local absolute paths.

## Rerun and recalibration rule

Rerun the retention/deletion gate whenever C3 durations, approval state, retention class identifiers, audit/timeline retention behavior, cleanup policy, tenant-deletion disposition, release evidence paths, or package-publish policy changes. A release recalibration must update C3, the tenant-deletion runbook, governance evidence, conformance tests, package-gate validation, and the latest report together.

## Submodule policy

Use only root-level submodule initialization:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Do not initialize nested submodules for this lane.
