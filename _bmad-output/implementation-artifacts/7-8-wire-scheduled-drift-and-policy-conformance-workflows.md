---
baseline_commit: d003e60
discovered_inputs:
  sprint_status: _bmad-output/implementation-artifacts/sprint-status.yaml
  epics: _bmad-output/planning-artifacts/epics.md
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
  project_context:
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
  previous_story: _bmad-output/implementation-artifacts/7-7-add-capacity-smoke-ci-gate.md
  related_policy_story: _bmad-output/implementation-artifacts/7-1-deploy-production-dapr-deny-by-default-access-control.md
  latest_technical_sources:
    - https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule
    - https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax
    - https://docs.dapr.io/operations/configuration/invoke-allowlist/
    - https://docs.dapr.io/reference/resource-specs/configuration-schema/
    - https://www.oasdiff.com/docs/breaking-changes
---

# Story 7.8: Wire scheduled drift and policy-conformance workflows

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want scheduled drift and policy-conformance workflows separate from PR CI,
so that live provider drift and production policy regressions are caught continuously.

## Acceptance Criteria

> Epic 7.8 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given provider contract and Dapr policy tests exist
> When scheduled workflows run
> Then nightly drift and policy-conformance results are reported with clear failure categories
> And breaking provider drift or unauthorized policy changes fail the workflow.

Decomposed acceptance criteria:

1. Add scheduled workflow files separate from `.github/workflows/ci.yml`, recommended paths `.github/workflows/nightly-drift.yml` and `.github/workflows/policy-conformance.yml`, with stable job/check names for branch protection or release-readiness review.
2. Workflows use `schedule` plus `workflow_dispatch`. Schedule timing is UTC/default-branch based, and manual dispatch accepts bounded inputs where useful, such as provider/version profile or policy mode.
3. Nightly provider drift reuses existing Forgejo drift fixtures and manifest: `tests/contracts/forgejo/supported-versions.json`, `tests/contracts/forgejo/*/swagger.v1.json`, `tests/tools/forgejo-drift/classification-fixtures.json`, `tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1`, and `ForgejoManifestAndDriftTests`.
4. The drift workflow reports clear categories, at minimum `forgejo-manifest-integrity`, `forgejo-snapshot-coverage`, `forgejo-drift-classification`, `forgejo-sanitized-report`, and a reference-pending or implemented `live-provider-drift` category. Breaking or unclassified drift fails; additive-compatible drift may warn but must still be visible in metadata-only results.
5. Policy-conformance workflow reuses existing Dapr production policy artifacts and static gate: `deploy/dapr/production/*.yaml`, `tests/fixtures/dapr-policy-conformance.yaml`, `DaprPolicyConformanceTests`, `tests/tools/run-dapr-policy-conformance-gates.ps1`, and `docs/operations/dapr-policy-conformance.md`.
6. The policy workflow reports clear categories, at minimum `static-policy-shape`, `fixture-provenance`, `negative-triple-coverage`, `mtls-and-sidecar-bindings`, `pubsub-topic-scopes`, and a reference-pending or implemented `live-kind-dapr-denial` category. Unauthorized policy changes fail the workflow.
7. If a live Dapr/kind execution lane is implemented now, it must use synthetic apps and operations only and prove denied service invocation returns the expected forbidden outcome. If environment support is not reliable, retain the static gate and mark live execution as `reference_pending_story_7_8` with an exact owner, command shape, evidence path, and follow-up boundary.
8. Both scheduled workflows keep the Epic 7 setup posture: `actions/checkout@v6`, `submodules: false`, explicit root-level submodule initialization only, `actions/setup-dotnet@v5` with `global-json-file: global.json`, stable NuGet cache inputs, `permissions: contents: read`, no package/container publishing, no release upload, no semantic-release, no broad artifact upload, and no nested recursive submodule setup.
9. Reports are metadata-only and repository-relative under `_bmad-output/gates/nightly-drift/latest.json` and `_bmad-output/gates/policy-conformance/latest.json` or another documented stable path. Reports may include gate name, schedule/manual trigger metadata, category names, provider/version classes, synthetic policy case IDs, artifact paths, status, severity, and exit codes only.
10. Static conformance tests prove the workflows, scripts, reports, documentation, and root-only submodule policy stay wired and scoped. Tests must fail closed on vacuous `dotnet test --filter` selections, missing reports, missing fixture inputs, missing workflow triggers, missing schedule/manual dispatch, or accidental movement of PR CI lanes, package publishing, container publishing, release upload, or secrets into these scheduled workflows.
11. Maintainer documentation records schedule cadence, manual dispatch usage, stable check names, local commands, report paths, failure categories, diagnostic policy, escalation/ownership, and how scheduled evidence feeds later release stories 7.12, 7.15, 7.16, and 7.17.
12. Existing PR CI jobs and focused gates remain usable and are not weakened: `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `run-dapr-policy-conformance-gates.ps1`, `run-container-image-gates.ps1`, `run-contract-spine-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-safety-invariant-gates.ps1`, and their documentation.

## Tasks / Subtasks

- [x] Add separate scheduled workflows (AC: 1, 2, 8, 12)
  - [x] Add `.github/workflows/nightly-drift.yml` with `schedule` and `workflow_dispatch`, stable job/name `nightly-drift-gates`, and no PR/push trigger unless deliberately documented as a dry-run validation path.
  - [x] Add `.github/workflows/policy-conformance.yml` with `schedule` and `workflow_dispatch`, stable job/name `policy-conformance-gates`, and no PR/push trigger unless deliberately documented as dry-run validation.
  - [x] Use the established Epic 7 checkout/setup pattern: `actions/checkout@v6` with `submodules: false`, explicit non-recursive root-level submodule init, `actions/setup-dotnet@v5`, `global-json-file: global.json`, and NuGet cache dependency paths.
  - [x] Keep `.github/workflows/ci.yml` focused on PR lanes from Stories 7.4 through 7.7; do not fold scheduled drift or live policy validation into PR CI.
  - [x] Do not add package publishing, container publishing, release upload, semantic-release, production deployment, Playwright install, broad artifact upload, or recursive/nested submodule setup.

- [x] Create or harden the nightly drift gate script (AC: 3, 4, 9, 10)
  - [x] Suggested path: `tests/tools/run-nightly-drift-gates.ps1`.
  - [x] Follow existing PowerShell gate style: `#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, repository-root resolution from script path, `$LASTEXITCODE` propagation, and `utf8NoBOM` JSON report output.
  - [x] Reuse `ForgejoManifestAndDriftTests` and `tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1`; do not create a second Forgejo manifest parser or drift classifier in the workflow YAML.
  - [x] Categories should include `forgejo-manifest-integrity`, `forgejo-snapshot-coverage`, `forgejo-drift-classification`, `forgejo-sanitized-report`, and `live-provider-drift`.
  - [x] Fail closed on missing manifest, missing snapshot, stale integrity hash, missing classification fixtures, unknown/unclassified change kind, breaking-incompatible classification, missing sanitized report, raw schema diff retention, forbidden sentinel values, missing test assembly, or zero/partial test selection.
  - [x] If live provider calls are not implemented in this story, write `live-provider-drift: reference_pending_story_7_8` with exact follow-up evidence fields; do not pretend static snapshots are live drift.

- [x] Create or wrap the scheduled policy-conformance script (AC: 5, 6, 7, 9, 10)
  - [x] Suggested path: `tests/tools/run-scheduled-policy-conformance-gates.ps1`, wrapping `run-dapr-policy-conformance-gates.ps1` for static checks and optionally invoking a live kind/daprd lane.
  - [x] Preserve the existing `run-dapr-policy-conformance-gates.ps1` behavior and report; do not break its use from `.github/workflows/contract-spine.yml`.
  - [x] Categories should include `static-policy-shape`, `fixture-provenance`, `negative-triple-coverage`, `mtls-and-sidecar-bindings`, `pubsub-topic-scopes`, and `live-kind-dapr-denial`.
  - [x] Fail closed on missing `deploy/dapr/production/accesscontrol.yaml`, `daprsystem.yaml`, `pubsub.yaml`, `secretstore.yaml`, `sidecar-config-bindings.yaml`, missing `tests/fixtures/dapr-policy-conformance.yaml`, stale semantic hash, missing negative categories, wildcard allow rules, missing mTLS evidence, unbound sidecars, unsafe pub/sub scopes, missing test assembly, or zero/partial test selection.
  - [x] If a live kind/daprd lane is added, keep it synthetic-only and isolated from production endpoints/secrets; report only synthetic case IDs, target app IDs, denied/allowed class, status category, and exit code.

- [x] Add static workflow/script/report conformance coverage (AC: 1-12)
  - [x] Add focused tests, suggested `tests/Hexalith.Folders.Contracts.Tests/Deployment/ScheduledDriftAndPolicyWorkflowConformanceTests.cs`.
  - [x] Parse workflow YAML with YamlDotNet and assert both workflows include `schedule`, `workflow_dispatch`, stable job names, setup-dotnet with `global-json-file: global.json`, root-level-only submodule initialization, and calls to the expected gate scripts.
  - [x] Assert workflow permissions stay `contents: read` or stricter and do not request packages, deployments, id-token, pull-requests, checks, or statuses write unless a documented reporting need is added with tests.
  - [x] Assert scripts contain exact category inventories, report paths, prerequisite checks, metadata-only policy, failure severities, and test-selection drift guards.
  - [x] Assert generated reports, when present, contain only metadata-only fields and repository-relative paths.
  - [x] Assert no recursive submodule setup appears in `.github`, `tests/tools`, `docs`, `deploy`, or `src` except guard/test assertions.
  - [x] Assert the scheduled workflows do not call PR-only focused gate scripts except the policy wrapper's intentional static Dapr gate reuse, and do not run package/container/release lanes.

- [x] Document maintainer and release-reviewer handoff (AC: 9, 11, 12)
  - [x] Add `docs/operations/scheduled-drift-and-policy-conformance.md` or split into clearly linked drift and policy docs if existing docs become too dense.
  - [x] Record stable check names, schedules, workflow_dispatch inputs, local commands, categories, report paths, metadata-only diagnostic policy, failure severity semantics, and owner/escalation.
  - [x] State how additive provider drift, breaking provider drift, unauthorized Dapr policy change, and live-kind unavailability are classified.
  - [x] Explain the relationship to `contract-spine.yml`: existing static policy conformance remains available; 7.8 scheduled workflows are continuous release-readiness evidence, not replacement PR CI.
  - [x] Include the exact root-level submodule command and state that nested recursive initialization is forbidden unless explicitly requested.

- [x] Preserve release-readiness boundaries (AC: 11, 12)
  - [x] Do not publish packages, build container images, upload release artifacts, update semantic versions, or create release notes in this story.
  - [x] Do not change provider adapter behavior, production Dapr allow rules, Forgejo supported-version policy, or redaction fixtures unless a focused conformance failure proves the artifact is stale and the change remains inside this story's scope.
  - [x] If updating `docs/exit-criteria/c0-c13-governance-evidence.yaml`, record only scheduled evidence commands/paths and keep unrelated C1/C2/C5/C12/C13 approvals unchanged unless their owning story has completed.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1`.
  - [x] Run `pwsh ./tests/tools/run-nightly-drift-gates.ps1`.
  - [x] Run `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1`.
  - [x] Run the new workflow/script/report conformance tests.
  - [x] Run the focused Forgejo drift tests, especially `ForgejoManifestAndDriftTests`.
  - [x] Run the focused Dapr policy tests, especially `DaprPolicyConformanceTests`.
  - [x] Run `git diff --check`.
  - [x] Run `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` and confirm new work did not introduce recursive setup outside guard/test assertions.
  - [x] If VSTest sockets, kind/daprd, or provider network calls are blocked in the sandbox, record the exact command, failure, and closest passing evidence in the Dev Agent Record without marking the blocked command as passed.

## Dev Notes

### Critical Scope Boundaries

- This story wires scheduled/continuous validation. It does not add product endpoints, REST/SDK/CLI/MCP commands, UI features, package publishing, container publishing, semantic release, production deployment, provider adapter behavior, or Dapr policy semantics unless a narrow fixture/gate correction is necessary.
- Keep scheduled workflows separate from PR CI. Story 7.4 owns baseline, 7.5 owns contract/parity, 7.6 owns security/redaction, 7.7 owns capacity smoke, and 7.8 owns scheduled provider drift plus policy conformance.
- Do not duplicate drift or policy logic in workflow YAML. YAML should set up the environment and call focused scripts; scripts/tests/fixtures remain source of truth.
- Do not fabricate live evidence. Static Forgejo snapshot and Dapr policy checks are valuable but not the same as live provider drift or live Dapr/kind denial. If the live lane is deferred, label it `reference_pending_story_7_8` with concrete follow-up evidence.
- Do not initialize nested submodules recursively. The allowed setup command is root-level only:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

### Current State To Preserve

- `.github/workflows/ci.yml` currently contains four PR jobs: `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, and `capacity-smoke-gates`. Do not weaken or merge them.
- `.github/workflows/contract-spine.yml` still runs restore/build plus contract spine, safety invariant, governance completeness, and Dapr policy conformance gates. Preserve Dapr coverage there unless the replacement is intentionally documented and tested.
- `tests/tools/run-dapr-policy-conformance-gates.ps1` writes `_bmad-output/gates/dapr-policy-conformance/latest.json` and records `live_dapr_kind_gate = reference_pending_story_7_8`. Story 7.8 should close or explicitly carry that reference-pending field into the scheduled evidence.
- `docs/operations/dapr-policy-conformance.md` already states the live Dapr/kind gate remains a promotion or scheduled validation lane for Story 7.8.
- Forgejo drift groundwork already exists: pinned snapshots under `tests/contracts/forgejo/`, `supported-versions.json`, `classification-fixtures.json`, and `Write-SanitizedForgejoDriftReport.ps1`.
- `ForgejoManifestAndDriftTests` already validates manifest integrity, snapshot coverage, operation coverage, classification fixture severity, sanitized report script presence, and forbidden sentinel scanning.
- Existing Epic 7 gate scripts use metadata-only reports under `_bmad-output/gates/<gate>/latest.json`; follow that pattern for scheduled drift/policy reports.

### Architecture Compliance

- Architecture C12 requires provider version pinning plus runtime capability-drift detection cadence and upgrade ritual; the provider contract suite runs in hermetic PR mode and live nightly drift mode.
- Architecture I-3 requires Dapr policy conformance with deny-by-default + mTLS, negative unauthorized triples, and policy YAML changes paired with negative-test updates.
- Architecture A-7 requires Forgejo per-version `swagger.v1.json` snapshots, `tests/contracts/forgejo/supported-versions.json`, nightly schema diff, and additive-vs-breaking classification.
- Architecture I-5 names GitHub Actions as the CI/CD mechanism and explicitly separates pipeline gates, nightly live-drift provider tests, Dapr policy conformance negative tests, and Forgejo schema-diff jobs.
- Production Dapr access control is applied to the called application's sidecar. Policy reasoning must use target app + caller app + operation + HTTP verb + namespace + trust domain, not a global firewall mental model.
- Metadata-only remains mandatory. Reports and diagnostics must not include file contents, raw diffs, provider payloads, access tokens, credential material, production URLs, stack traces, environment dumps, tenant data, local absolute paths, or unauthorized-resource hints.

### Latest Technical Notes

- GitHub scheduled workflows use POSIX cron syntax, default to UTC, run on the latest commit of the default branch, and have a shortest interval of once every five minutes. Use a nightly cadence that avoids top-of-hour congestion and add `workflow_dispatch` for manual reruns.
- GitHub `workflow_dispatch` only receives events when the workflow file is on the default branch; manual inputs should stay bounded and non-secret.
- GitHub workflow `permissions` should be set explicitly. For this story, `contents: read` is sufficient unless a later reporting decision is documented and tested.
- Dapr access control defaults to allow when no policy is specified, so deny-by-default policy and negative tests are load-bearing.
- Dapr access-control operations support wildcard names and verbs, but this repository forbids wildcard allow rules in production policy conformance unless a future test change deliberately creates a reviewed exception.
- Dapr `spec.mtls` belongs to the Dapr Configuration schema; do not infer mTLS from access-control `trustDomain` alone.
- oasdiff classifies OpenAPI breaking changes across many rule categories. For Story 7.8, use a bounded classifier aligned with existing Forgejo fixtures; do not upload raw upstream schema diffs or provider payloads into reports.

### Previous Story Intelligence

- Story 7.7 established the current Epic 7 pattern: add a separate stable workflow job, reuse checkout/setup posture, create a focused PowerShell orchestrator under `tests/tools`, emit a metadata-only report under `_bmad-output/gates/<gate>/latest.json`, add static conformance tests, document handoff, and preserve root-level submodule policy.
- Story 7.7 kept scheduled drift, Dapr policy, package publishing, release upload, and container image lanes outside the capacity PR job. Preserve that lane separation.
- Story 7.6 review found a real vacuous-pass trap: `dotnet test --filter` and the xUnit fallback can exit 0 with zero or partial test selection. Scheduled scripts must fail closed unless expected tests/categories actually execute.
- Story 7.1 created static Dapr policy conformance and intentionally left the live kind/daprd denial lane to Story 7.8. Do not lose that handoff.
- Recent commits show Epic 7 is consolidating focused release-readiness gates one at a time: `d003e60 feat(story-7.7): Add capacity-smoke CI gate`, `5e72383 feat(story-7.6): Consolidate security and redaction CI gates`, `d93f1fd feat(story-7.5): Consolidate contract and parity CI gates`, and `e9f59a3 feat(story-7.4): consolidate baseline build and unit CI gates`.

### Project Structure Notes

- Likely NEW files:
  - `.github/workflows/nightly-drift.yml`
  - `.github/workflows/policy-conformance.yml`
  - `tests/tools/run-nightly-drift-gates.ps1`
  - `tests/tools/run-scheduled-policy-conformance-gates.ps1`
  - `tests/Hexalith.Folders.Contracts.Tests/Deployment/ScheduledDriftAndPolicyWorkflowConformanceTests.cs`
  - `docs/operations/scheduled-drift-and-policy-conformance.md`
  - `_bmad-output/gates/nightly-drift/latest.json` generated by local/scheduled runs
  - `_bmad-output/gates/policy-conformance/latest.json` generated by local/scheduled runs
- Likely UPDATE files:
  - `docs/operations/dapr-policy-conformance.md` to replace the Story 7.8 handoff note with the actual scheduled workflow command/evidence.
  - `docs/contract/contract-parity-ci-gates.md` only if transitional references to Stories 7.6 and 7.8 become stale.
  - `docs/exit-criteria/c0-c13-governance-evidence.yaml` only to record scheduled evidence paths/commands without prematurely approving unrelated criteria.
  - `tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1` only if the existing report shape cannot satisfy scheduled evidence requirements.
  - `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoManifestAndDriftTests.cs` only if additional fail-closed drift evidence is needed.
  - `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` only if scheduled conformance needs exact-count/report assertions not already covered elsewhere.
- Do not update generated clients, OpenAPI spine, parity rows, security fixtures, Dapr production allow rules, Forgejo pinned versions, package versions, release artifacts, or container-image metadata unless a focused 7.8 gate failure proves a source artifact is stale and the change is explicitly in scope.

### Testing Requirements

- Use repository-pinned .NET SDK `10.0.302` from `global.json` and central package versions from `Directory.Packages.props`.
- Use xUnit v3, Shouldly, and YamlDotNet patterns already present in deployment conformance tests.
- Prefer focused verification first: new scheduled scripts, new deployment conformance tests, `ForgejoManifestAndDriftTests`, and `DaprPolicyConformanceTests`.
- Conformance tests should parse YAML rather than relying on string-only workflow assertions where practical.
- Scheduled scripts should follow the Story 7.6/7.7 fail-closed pattern for `dotnet test --filter`: exact expected test count or runner method inventory, with an xUnit in-process fallback for sandbox VSTest socket denial.
- If kind/daprd live execution is implemented, isolate it from normal PR/unit lanes and keep diagnostics synthetic and metadata-only. If unavailable in the sandbox, record it as blocked evidence, not passed evidence.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.8`] - Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Exit-Criteria-Operations-Plan`] - C12 provider drift detection artifact location and live-nightly requirement.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Architecture-Decisions`] - A-7 Forgejo snapshots/drift classification, I-3 Dapr policy conformance, and I-5 GitHub Actions gate separation.
- [Source: `_bmad-output/planning-artifacts/prd.md#Provider-Readiness-And-Contract-Testing`] - GitHub and Forgejo provider contracts must be validated and drift made visible.
- [Source: `_bmad-output/project-context.md#Development-Workflow-Rules`] - Root-level submodules only; no recursive initialization.
- [Source: `_bmad-output/project-context.md#Testing-Rules`] - Focused gate scripts are CI contracts and must stay hermetic/metadata-only.
- [Source: `_bmad-output/implementation-artifacts/7-1-deploy-production-dapr-deny-by-default-access-control.md#Architecture-Compliance`] - Static Dapr conformance exists; live Dapr/kind gate is Story 7.8's scheduled/promotion handoff.
- [Source: `_bmad-output/implementation-artifacts/7-7-add-capacity-smoke-ci-gate.md#Previous-Story-Intelligence`] - Epic 7 focused gate pattern and lane separation.
- [Source: `.github/workflows/ci.yml`] - Current PR CI jobs that must remain separate from scheduled workflows.
- [Source: `.github/workflows/contract-spine.yml`] - Existing Dapr static conformance invocation that must not be weakened accidentally.
- [Source: `tests/tools/run-dapr-policy-conformance-gates.ps1`] - Static Dapr policy conformance gate and current live-kind reference-pending field.
- [Source: `docs/operations/dapr-policy-conformance.md`] - Existing operations handoff explicitly naming Story 7.8 as live/scheduled policy validation owner.
- [Source: `tests/contracts/forgejo/supported-versions.json`] - Supported Forgejo version manifest and snapshot inventory.
- [Source: `tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1`] - Existing sanitized Forgejo drift report writer.
- [Source: `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoManifestAndDriftTests.cs`] - Existing Forgejo manifest, drift-classification, and redaction tests.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`] - Existing static Dapr policy shape, fixture, mTLS, sidecar, and pub/sub tests.
- [Source: GitHub Docs, events that trigger workflows, checked 2026-05-30] - Scheduled workflows use cron, default UTC, default-branch latest commit, and minimum five-minute interval. https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule
- [Source: GitHub Docs, workflow syntax, checked 2026-05-30] - Explicit `GITHUB_TOKEN` permissions and `workflow_dispatch` inputs. https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax
- [Source: Dapr Docs, service invocation access control, checked 2026-05-30] - Access-control policies apply to called app sidecars; default behavior is allow without policy; operations can constrain path and HTTP verb. https://docs.dapr.io/operations/configuration/invoke-allowlist/
- [Source: Dapr Docs, Configuration spec, checked 2026-05-30] - `spec.mtls` is the mTLS configuration field. https://docs.dapr.io/reference/resource-specs/configuration-schema/
- [Source: oasdiff breaking change docs, checked 2026-05-30] - Breaking-change classification should distinguish client-impacting OpenAPI changes. https://www.oasdiff.com/docs/breaking-changes

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-30: Created story context from BMAD `bmad-create-story` workflow, Story 7.8 request, Epic 7 BDD, PRD provider/Dapr release-readiness requirements, architecture C12/I-3/I-5/A-7 guidance, project context, current CI workflows/scripts, existing Forgejo drift fixtures/tests, existing Dapr policy conformance artifacts/tests, Story 7.1 handoff, Story 7.7 previous-story intelligence, and recent git history.
- 2026-05-30: Validation pass checked story for common implementation traps: mixing scheduled live validation into PR CI, duplicating provider/policy logic in YAML, treating static snapshot checks as live drift evidence, fabricating live Dapr/kind pass evidence, leaking raw provider schema diffs or Dapr diagnostics, missing fail-closed test selection guards, recursive submodule setup, and accidental package/container/release publishing.
- 2026-05-30: Implemented scheduled `nightly-drift-gates` and `policy-conformance-gates` workflows, focused gate scripts, metadata-only reports, static conformance tests, and maintainer documentation.
- 2026-05-30: VSTest is blocked in this sandbox by `System.Net.Sockets.SocketException (13): Permission denied` from `Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.SocketServer.Start`; focused validation used the repository's xUnit v3 self-executable fallback and fail-closed exact-count checks.
- 2026-05-30: Full `dotnet test Hexalith.Folders.slnx --no-build -m:1 -v minimal` was attempted and blocked by the same VSTest socket permission issue before executing test cases.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story is scoped to scheduled drift and policy-conformance workflow wiring with focused scripts, metadata-only reports, static conformance tests, documentation, and explicit live-evidence boundaries.
- Existing PR CI lanes, Dapr static conformance gate, Forgejo drift fixtures, and root-only submodule policy are preserved as implementation constraints.
- Added separate scheduled GitHub Actions workflows with UTC schedules, bounded manual dispatch inputs, stable job names, read-only permissions, root-level submodule initialization, setup-dotnet from `global.json`, and no PR/push/package/container/release lanes.
- Added `run-nightly-drift-gates.ps1` and `run-scheduled-policy-conformance-gates.ps1` reports under `_bmad-output/gates/nightly-drift/latest.json` and `_bmad-output/gates/policy-conformance/latest.json`; live provider and live kind lanes are explicitly `reference_pending_story_7_8`.
- Hardened `run-dapr-policy-conformance-gates.ps1` with `-m:1` restore/build and xUnit fallback support so the scheduled wrapper can run in the sandbox while preserving the existing static gate/report contract.
- Added `ScheduledDriftAndPolicyWorkflowConformanceTests` covering workflow triggers, setup posture, permissions, category inventories, metadata-only report shape, PR lane separation, and recursive-submodule policy.
- Documented schedules, manual dispatch, commands, categories, report paths, ownership/escalation, relationship to `contract-spine.yml`, and release-story handoff in `docs/operations/scheduled-drift-and-policy-conformance.md`.
- Verification passed for restore, build, both scheduled scripts, focused scheduled conformance tests, focused Forgejo drift tests, focused Dapr policy tests, `git diff --check`, and recursive-submodule scan; full VSTest regression execution is sandbox-blocked as noted above.

### File List

- `.github/workflows/nightly-drift.yml`
- `.github/workflows/policy-conformance.yml`
- `_bmad-output/gates/dapr-policy-conformance/latest.json`
- `_bmad-output/gates/nightly-drift/latest.json`
- `_bmad-output/gates/nightly-drift/sanitized-forgejo-drift.json`
- `_bmad-output/gates/policy-conformance/latest.json`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/7-8-wire-scheduled-drift-and-policy-conformance-workflows.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/implementation-artifacts/tests/7-8-test-summary.md`
- `docs/operations/dapr-policy-conformance.md`
- `docs/operations/scheduled-drift-and-policy-conformance.md`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ScheduledDriftAndPolicyWorkflowConformanceTests.cs`
- `tests/tools/run-dapr-policy-conformance-gates.ps1`
- `tests/tools/run-nightly-drift-gates.ps1`
- `tests/tools/run-scheduled-policy-conformance-gates.ps1`

### Change Log

- 2026-05-30: Added scheduled drift and policy conformance workflow wiring, focused scripts, metadata-only reports, conformance tests, and operations documentation for Story 7.8.
- 2026-05-30: Preserved PR CI boundaries and static Dapr conformance while carrying live provider/kind evidence as explicit `reference_pending_story_7_8` handoffs.
- 2026-05-30: Senior Developer Review (auto-fix) — fixed `run-scheduled-policy-conformance-gates.ps1` to restore/build before asserting the test assembly (documented local command now works from a clean checkout), completed the File List, and recorded the review outcome.

## Senior Developer Review (AI)

**Reviewer:** Jerome (automated adversarial review) on 2026-05-30
**Outcome:** Approve — 0 critical issues; story acceptance criteria 1-12 verified against the implementation.

### Verification evidence (this session)

- `ScheduledDriftAndPolicyWorkflowConformanceTests`: 7/7 passed (in-process xUnit runner).
- `DaprPolicyConformanceTests`: 8/8 passed (VSTest).
- `ForgejoManifestAndDriftTests`: 7/7 passed.
- `pwsh ./tests/tools/run-nightly-drift-gates.ps1 -SkipRestoreBuild`: exit 0, report `passed`, metadata-only.
- `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1 -SkipRestoreBuild`: exit 0, report `passed`, static gate `passed`.
- All three gate reports (`nightly-drift`, `policy-conformance`, `dapr-policy-conformance`) regenerated with `status: passed` and pass the metadata-only recursive scan.

### Findings and resolution

- **MEDIUM (fixed):** `run-scheduled-policy-conformance-gates.ps1` called `Assert-TestAssembly` before any build in the non-`-SkipRestoreBuild` path (build was delegated to the static gate, which runs later), so the documented local command `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1` failed closed with `missing-test-assembly` on a clean checkout. Now restores/builds first (mirroring `run-nightly-drift-gates.ps1`) and always invokes the static gate with `-SkipRestoreBuild` to avoid a redundant build. CI behavior (workflow passes `-SkipRestoreBuild`) is unchanged.
- **LOW (fixed):** Story File List omitted the 7.8 test-summary artifacts; added them.
- **LOW (noted, out of 7.8 scope):** The static gate report `canonical_inputs` in `run-dapr-policy-conformance-gates.ps1` omits `deploy/dapr/production/secretstore.yaml`, although it is a canonical production input (AC5) covered by `ProductionSecretStoreArtifactsShouldBeReferenceOnlyAndDenyByDefault`. The scheduled wrapper's report already lists it. Left unchanged to honor the story's "preserve the existing static gate report" boundary; recommend the owning story align it.
- **Out of scope (not a 7.8 regression):** The full `Hexalith.Folders.Contracts.Tests` assembly has 4 pre-existing failures (`CommitStatusContractGroupTests`, `FileContextContractGroupTests`, `TenantFolderProviderContractGroupTests`, `AuditOpsConsoleContractGroupTests` negative-scope guards). These scan only `src/` (clean vs HEAD; untouched by 7.8) and fail because the project has legitimately grown CLI/MCP capability source past the Story 1.10-era negative-scope assertions. They fail identically at baseline `d003e60`. Recommend a correct-course/refresh of those guards under their owning stories.
