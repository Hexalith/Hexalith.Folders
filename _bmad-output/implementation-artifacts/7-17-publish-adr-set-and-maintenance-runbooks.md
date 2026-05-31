---
baseline_commit: e535995
discovered_inputs:
  sprint_status: _bmad-output/implementation-artifacts/sprint-status.yaml
  epics: _bmad-output/planning-artifacts/epics.md
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  previous_story: _bmad-output/implementation-artifacts/7-16-publish-nfr-traceability-bridge.md
  authoritative_sources:
    - _bmad-output/planning-artifacts/architecture.md
    - _bmad-output/planning-artifacts/epics.md
    - _bmad-output/planning-artifacts/prd.md
    - docs/adrs/0000-template.md
    - docs/adrs/0001-folder-domain-processor-persistence.md
    - docs/runbooks/tenant-deletion.md
    - docs/exit-criteria/c0-c13-governance-evidence.yaml
    - docs/exit-criteria/c3-retention.md
    - docs/exit-criteria/nfr-traceability.md
    - docs/operations/incident-alerting-and-recovery.md
    - docs/operations/production-observability.md
    - docs/operations/canonical-error-catalog.md
    - docs/operations/provider-integration-and-testing.md
    - docs/operations/scheduled-drift-and-policy-conformance.md
    - docs/operations/release-packages.md
    - docs/operations/container-images-and-dapr-app-ids.md
    - docs/operations/retention-and-tenant-deletion.md
    - docs/operations/operations-console.md
    - tests/tools/run-provider-error-docs-gates.ps1
    - tests/tools/run-nfr-traceability-gates.ps1
    - tests/tools/run-baseline-ci-gates.ps1
    - .github/workflows/contract-spine.yml
    - tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs
---

# Story 7.17: Publish ADR set and maintenance runbooks

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a future maintainer or architect,
I want ADRs and lifecycle runbooks published,
so that design rationale and operational decisions survive handoff and release pressure.

## Acceptance Criteria

Epic 7.17 BDD from `_bmad-output/planning-artifacts/epics.md`:

Given MVP release evidence is complete
When ADRs and runbooks are reviewed
Then ADRs cover major contract, provider, idempotency, security, observability, and deployment decisions
And runbooks cover tenant deletion, retention, alerts, rollback, provider drift, reconciliation, and incident-mode operations.

Decomposed acceptance criteria:

1. Publish an Architecture Decision Record (ADR) set under `docs/adrs/` that captures the six already-accepted major decisions named in the BDD: **contract**, **provider**, **idempotency**, **security**, **observability**, and **deployment**. These are retrospective ADRs that document decisions already implemented across Epics 1-7; they must record the decision as `Accepted`, not propose new design.
2. Author exactly six new numbered ADRs, `0002`-`0007`, one per decision area, each copied from the `docs/adrs/0000-template.md` structure and each replacing every `PLACEHOLDER` with real content. Required sections per ADR (following the `0000-template.md` shape, which the older `0001` predates): a title (`# ADR NNNN: <decision>`), a leading `Date:` metadata line (the `Date: YYYY-MM-DD` form used by `0001`, not a `## Date` header), `## Status` whose body contains `Accepted`, `## Context`, `## Decision`, `## Consequences`, `## Alternatives Considered`, and `## Verification`. Each ADR must cite at least one architecture decision identifier that exists in `_bmad-output/planning-artifacts/architecture.md` (for example contract→`C0`/`A-1`, provider→`A-6`/`A-7`/`C12`, idempotency→`A-9`/`D-7`, security→`S-2`/`S-4`/`S-6`, observability→`I-6`/`I-7`, deployment→`I-2`/`I-3`/`I-4`) and at least one implementing story or epic.
3. The `docs/adrs/0000-template.md` file must remain an unmodified, non-policy placeholder: it must keep `non_policy_placeholder: true`, keep its `PLACEHOLDER` markers, and must not be converted into a completed ADR. The existing `docs/adrs/0001-folder-domain-processor-persistence.md` must be preserved unchanged.
4. Publish a maintenance runbook set under `docs/runbooks/` covering the seven topics named in the BDD: **tenant deletion**, **retention**, **alerts**, **rollback**, **provider drift**, **reconciliation**, and **incident-mode operations**. Each topic maps to exactly one runbook file. `docs/runbooks/tenant-deletion.md` already exists and is the tenant-deletion runbook — preserve it and cross-link it; do not duplicate its disposition matrix. Author the remaining six runbooks: `retention.md`, `alerts.md`, `rollback.md`, `provider-drift.md`, `reconciliation.md`, `incident-mode.md`.
5. Each of the **six new** runbooks (every runbook except the preserved `tenant-deletion.md`) must be operator-facing and follow a consistent section contract: `## Purpose`/scope, `## Preconditions` (authorization prerequisites where a tenant boundary applies), a `## Procedure` or decision-tree section with concrete operator steps, `## Verification`, `## Escalation and handoff`, a `## Related evidence` cross-link section, and a metadata-only `## Forbidden evidence` clause. The pre-existing `tenant-deletion.md` is preserved with its own established section set (`## Authorization prerequisites`, `## Disposition matrix`, `## Manual review checklist`, `## Automated validation`, `## Forbidden evidence`) — do NOT retrofit the new-runbook contract onto it. The new runbooks must cross-link the existing operations/exit-criteria evidence rather than restating it: retention→`docs/exit-criteria/c3-retention.md` + `docs/operations/retention-and-tenant-deletion.md`; alerts→`docs/operations/incident-alerting-and-recovery.md` + `docs/operations/production-observability.md`; rollback→`docs/operations/release-packages.md` + `docs/operations/container-images-and-dapr-app-ids.md`; provider drift→`docs/operations/scheduled-drift-and-policy-conformance.md` + `docs/operations/provider-integration-and-testing.md`; reconciliation→`docs/operations/canonical-error-catalog.md` + `_bmad-output/planning-artifacts/architecture.md` C6 transition matrix; incident-mode→`docs/operations/incident-alerting-and-recovery.md` + `docs/operations/operations-console.md`.
6. The runbooks must author the three genuine operator gaps that existing docs do not yet cover: (a) **rollback** — step-by-step release/container revert and post-rollback health verification; (b) **reconciliation** — the operator decision tree for `unknown_provider_outcome` / `reconciliation_required` with an explicit no-silent-retry rule; (c) **incident-mode** — when to use the `/_admin/incident-stream` last-resort read path, how to interpret operator-disposition labels, and the escalation/handoff path. Alerts and provider-drift runbooks must reference live-tooling boundaries honestly (see AC9).
7. Publish an ADR index `docs/adrs/index.md` and a runbook index `docs/runbooks/index.md`. Each index must enumerate exactly the published ADRs / runbooks for its directory (one row per file) with no missing entries, no orphan entries pointing at non-existent files, and no duplicates. The ADR index must not list `0000-template.md` as an accepted decision.
8. Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs` using xUnit v3 + Shouldly + semantic Markdown/YAML parsing, mirroring the Story 7.13-7.16 doc-gate pattern. It must re-derive the required ADR decision-area inventory and runbook-topic inventory from a marker-bounded source-of-truth block in this story's gate (or a checked-in manifest) and assert exact equality against the published files — no hardcoded happy-path `Contains` that cannot fail closed.
9. The conformance test must assert, by parsing the real files: (a) all six new ADRs `0002`-`0007` exist, each has every required section, each `## Status` body **contains** the token `Accepted` (use a contains/starts-with check, not exact equality — the established `0001` house style is the prose form "Accepted for Story 2.8b implementation"), each contains no residual `PLACEHOLDER`, and each cites at least one architecture decision ID that is actually present in `architecture.md`; (b) the template `0000` still carries `non_policy_placeholder: true` and still contains `PLACEHOLDER`, and `0001` is preserved (its required-section set is its own, not the new-ADR contract); (c) the six new runbooks each have the AC5 new-runbook section contract, and the preserved `tenant-deletion.md` has its own existing section set (`## Authorization prerequisites`, `## Disposition matrix`, `## Manual review checklist`, `## Automated validation`, `## Forbidden evidence`) — assert these two section sets separately so neither file fails the other's contract; (d) the ADR and runbook indexes are inventory-exact against the directory contents (the ADR index lists `0001`-`0007` and excludes `0000-template.md`; the runbook index lists all seven topics); (e) every **new** ADR (`0002`-`0007`), every new runbook, and both indexes are metadata-only. Coverage must be non-vacuous: the six decision areas and seven runbook topics are each present exactly once.
10. The conformance test must include negative controls routed through the **same** real parsers/scanners used for the production checks (not tautological string checks): an ADR left as the `PLACEHOLDER` template, an ADR missing a required section, an ADR with `## Status` not `Accepted`, an ADR citing a decision ID absent from `architecture.md`, a missing runbook topic, a runbook missing a required section, an index with a missing entry, an index with an orphan entry, an unsafe local absolute path, a credential/token/bearer-like string, a non-placeholder production host, malformed Markdown/YAML, and a forbidden recursive `git submodule update --init --recursive` command.
11. Add a focused PowerShell gate `tests/tools/run-adr-runbook-docs-gates.ps1` that emits `_bmad-output/gates/adr-runbook-docs/latest.json`. Mirror the Story 7.15/7.16 posture exactly: `#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, `[Alias('NoRestore')][switch]$SkipRestoreBuild`, repository-root resolution from the script path, `Push-Location`/`finally` `Pop-Location`, `$LASTEXITCODE` propagation, `utf8NoBOM` report writes, a `$runnerMethods` array that is exactly equal to the `[Fact]` set, the VSTest-socket-denied fallback to the xUnit v3 in-process runner, a fail-closed vacuous-test guard (`executedTests < $runnerMethods.Count` fails), metadata-only diagnostics, and a bounded `surfaces[]` list.
12. Wire CI without broadening unrelated lanes: add the ADR/runbook docs gate step to `.github/workflows/contract-spine.yml` immediately after the `Run NFR traceability conformance gates` step, and append `FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests` to the Contracts.Tests filter in `tests/tools/run-baseline-ci-gates.ps1` (line 31). The conformance test must assert both wirings are present, and must assert that `.github/workflows/ci.yml`, `.github/workflows/nightly-drift.yml`, and `.github/workflows/policy-conformance.yml` do **not** run this focused gate (assert against the real filenames; verify the exact names on disk before pinning them in the test). Keep `submodules: false` and root-level-only submodule initialization on every modified surface.
13. Every new doc, ADR, runbook, index, test diagnostic, gate report, and example must be metadata-only and use synthetic data only. The metadata-only scanner targets the **new** artifacts (`0002`-`0007`, the new runbooks, both indexes, the gate report, and diagnostics); `0000-template.md` and `0001-*.md` are pre-existing exempt fixtures asserted only for placeholder-preservation (AC3/AC9(b)). Metadata-only forbids secrets, bearer tokens, JWTs, credential material, production URLs, tenant data, raw provider payloads, raw provider responses, raw file contents, diffs, stack traces, environment dumps, host-absolute paths, and generated report bodies that expose sensitive data — it does NOT forbid legitimate repository-relative paths or production code identifiers (e.g. type names like `FolderArchiveTenantGate`), which ADRs legitimately reference. Use repository-relative paths, opaque synthetic identifiers (for example `tenant-001`, `folder-001`, `operation-001`), safe placeholder hosts (`*.invalid`/`*.example`/`*.internal`/`*.localhost`/`*.test`), status values, decision IDs, and stable hashes. Ensure the scanner does not false-positive on these allowed forms.

## Tasks / Subtasks

- [x] Publish the ADR set (AC: 1, 2, 3, 13)
  - [x] Author `docs/adrs/0002-*.md` (contract) citing `C0`/`A-1` and the Epic 1 Contract Spine stories.
  - [x] Author `docs/adrs/0003-*.md` (provider) citing `A-6`/`A-7`/`C12` and the Epic 3 provider-adapter stories.
  - [x] Author `docs/adrs/0004-*.md` (idempotency) citing `A-9`/`D-7` and the Epic 1/4 idempotency stories.
  - [x] Author `docs/adrs/0005-*.md` (security) citing `S-2`/`S-4`/`S-6` and the Epic 2/7 authorization/OIDC stories.
  - [x] Author `docs/adrs/0006-*.md` (observability) citing `I-6`/`I-7`/`C2` and Story 7.12.
  - [x] Author `docs/adrs/0007-*.md` (deployment) citing `I-2`/`I-3`/`I-4` and Stories 7.1/7.3.
  - [x] Use the `0000-template.md` section structure; replace every `PLACEHOLDER`; set `## Status` = `Accepted`; leave `0000-template.md` and `0001-*.md` untouched.

- [x] Publish the maintenance runbook set (AC: 4, 5, 6, 13)
  - [x] Preserve `docs/runbooks/tenant-deletion.md`; cross-link it from the index.
  - [x] Author `docs/runbooks/retention.md` cross-linking `c3-retention.md` and `retention-and-tenant-deletion.md`.
  - [x] Author `docs/runbooks/alerts.md` cross-linking `incident-alerting-and-recovery.md` and `production-observability.md`, with the five Story 7.12 signals and an escalation checklist.
  - [x] Author `docs/runbooks/rollback.md` with release-package and container-image revert steps plus post-rollback health verification.
  - [x] Author `docs/runbooks/provider-drift.md` cross-linking `scheduled-drift-and-policy-conformance.md` and `provider-integration-and-testing.md`.
  - [x] Author `docs/runbooks/reconciliation.md` with the `unknown_provider_outcome` / `reconciliation_required` operator decision tree and the no-silent-retry rule.
  - [x] Author `docs/runbooks/incident-mode.md` for the `/_admin/incident-stream` last-resort read path, disposition-label interpretation, and escalation/handoff.
  - [x] Give every runbook the AC5 section contract (Purpose, Preconditions, Procedure/decision tree, Verification, Escalation and handoff, Related evidence, Forbidden evidence).

- [x] Publish the indexes (AC: 7)
  - [x] Author `docs/adrs/index.md` enumerating ADRs `0001`-`0007` (excluding `0000-template.md` as an accepted decision).
  - [x] Author `docs/runbooks/index.md` enumerating all seven runbooks mapped to their BDD topic.

- [x] Add the static conformance test (AC: 8, 9, 10, 13)
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs` as a `public sealed partial class` if `[GeneratedRegex]` helpers are used.
  - [x] Re-derive the six ADR areas and seven runbook topics, parse ADR/runbook/index files, and parse `architecture.md` decision IDs.
  - [x] Assert ADR completeness, `Accepted` status, no residual placeholder, real decision-ID citations, template-placeholder preservation, runbook completeness, inventory-exact indexes, and metadata-only posture.
  - [x] Add the AC10 negative controls, each routed through the same production parsers/scanners.

- [x] Add the focused gate and CI wiring (AC: 11, 12)
  - [x] Add `tests/tools/run-adr-runbook-docs-gates.ps1` and generate `_bmad-output/gates/adr-runbook-docs/latest.json`.
  - [x] Add the gate step to `.github/workflows/contract-spine.yml` after the NFR traceability step.
  - [x] Append the conformance FQN to the Contracts.Tests filter in `tests/tools/run-baseline-ci-gates.ps1` (line 31).
  - [x] Confirm `ci.yml`, scheduled drift, and policy-conformance workflows do not run the focused gate.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` if needed.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1`.
  - [x] Run the focused `AdrRunbookDocsConformanceTests`.
  - [x] Run `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1 -SkipRestoreBuild`.
  - [x] Run `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` and confirm the new conformance facts execute via the appended filter.
  - [x] Run `dotnet format whitespace` + `dotnet format analyzers` on the new test; run `git diff --check` and a recursive-submodule scan over executable/docs surfaces.

## Dev Notes

### Scope Boundaries

- This story is **documentation plus static conformance only**. It must not change product runtime behavior, OpenAPI operation semantics, generated client files, provider adapters, UI behavior, event schemas, parity oracle rows, capacity harness semantics, retention/deletion enforcement, observability wiring, or live provider credentials. The ADRs are retrospective: they record decisions already implemented in Epics 1-7, not new design.
- **Do not modify `docs/exit-criteria/nfr-traceability.md`, `NfrTraceabilityConformanceTests`, or `docs/exit-criteria/c0-c13-governance-evidence.yaml`.** The NFR bridge already cites `docs/runbooks/*.md` and names story `7-17` as the consuming owner for the runbook-proof and NFR54/NFR55 reference-pending gaps; publishing the runbooks under `docs/runbooks/` satisfies that pointer without rewriting the bridge. Editing those artifacts would entangle this story with the 7.16 gate for no scope benefit. The new gate may read-only assert that the NFR bridge references `docs/runbooks/` and `7-17`, but must not require editing it.
- **Honesty over narrative (mirrors Story 7.16 AC6).** Authoring runbook *documentation* does not by itself deliver live *tooling*. NFR54 (live alert-delivery tooling) and NFR55 (backup/restore + recovery-drill tooling) remain genuinely unavailable in MVP — the architecture and `incident-alerting-and-recovery.md` state there is no backup automation in MVP and recovery relies on durable events + rebuild. The alerts and rollback runbooks must document the *operator process* and explicitly mark live-delivery/backup-restore tooling and manual recovery-drill evidence as `reference_pending` (owner: Operations Runbook; consuming story `7-17`). Do not claim live tooling exists, and do not flip NFR54/NFR55/NFR62-66 to covered.
- **Do not broaden PR `ci.yml`, scheduled drift, or policy workflows.** The focused static gate belongs in `contract-spine.yml` and the baseline Contracts.Tests filter, exactly like the 7.13-7.16 doc gates. No release-package wiring is required for this story (runbook release-blocking semantics already flow through the already-wired NFR traceability gate); do not add this gate to `release-packages.yml` or `run-release-package-gates.ps1`.
- Do not initialize nested submodules. Every modified script/workflow/doc must keep `submodules: false` and root-level-only initialization. The exact allowed command is `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`.

### Implementation Pattern (reuse, do not reinvent)

- Mirror the Story 7.13-7.16 doc-gate pattern precisely: published docs + one `Deployment/*ConformanceTests.cs` + one `tests/tools/run-*-gates.ps1` + one `_bmad-output/gates/<gate>/latest.json` + one `contract-spine.yml` step + one baseline Contracts.Tests filter append.
- Recommended identities: gate `adr-runbook-docs`; script `tests/tools/run-adr-runbook-docs-gates.ps1`; report `_bmad-output/gates/adr-runbook-docs/latest.json`; conformance class `AdrRunbookDocsConformanceTests`.
- **Copy the gate script from `tests/tools/run-provider-error-docs-gates.ps1` or `run-nfr-traceability-gates.ps1`** and adapt names/paths/`$runnerMethods`. Both already implement the required `$SkipRestoreBuild` alias, repo-root resolution, `Write-*Report` helper with `utf8NoBOM`, the `Get-ExecutedTestCount`/`Invoke-XunitInProcessFallback` socket-denied fallback, the TRX vacuous-test guard, and the `discovered`→`passed`/`failed` report lifecycle. Do not author a new gate shape from scratch.
- **Copy the conformance-test skeleton from `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs`** (its path constants, `AllowedPlaceholderHostSuffixes`, `SubmoduleCommand`, marker-bounded table parsing, set-equality helpers, and metadata-only scanner are directly reusable). `OperationsAuditDocsConformanceTests.cs` is the closest precedent for prose-doc section parsing.
- Use marker comments inside the indexes (for example `<!-- adr-index -->` / `<!-- runbook-index -->`) so the test parses exact blocks and can fail closed on inventory drift, exactly as the NFR table uses `<!-- nfr-traceability-table -->`.
- Surfaces array for the gate report should be bounded, e.g.: `adr-set`, `adr-template-preserved`, `runbook-set`, `adr-index`, `runbook-index`, `architecture-decision-citations`, `metadata-only`, `ci-wiring`.

### ADR area → architecture decision mapping (source-pinned)

Re-derive citations from `_bmad-output/planning-artifacts/architecture.md`; do not invent IDs. Confirmed decision anchors:

- **Contract** (`0002`): `C0` / `A-1` Contract Spine = single source of truth, OpenAPI 3.1 owned by `Hexalith.Folders.Contracts`, NSwag-generated SDK with `ComputeIdempotencyHash()` helpers, server/parity/generated-client kept in sync (`A-2`, `A-3`, `C13`). Implementing epic: Epic 1.
- **Provider** (`0003`): `A-6` GitHub via Octokit `14.0.0`; `A-7` Forgejo via typed `HttpClient` + per-version snapshots; `IGitProvider` port + `ProviderOperationCatalog` capability model; nightly oasdiff drift (`C12`); "Forgejo is not a GitHub base-URL swap". Implementing epic: Epic 3.
- **Idempotency** (`0004`): `A-9` per-command canonical hash over fields in lexicographic order via `x-hexalith-idempotency-equivalence`; SHA-256 + NFC + length-prefix + duplicate-property rejection + fingerprint ledger; same-key/equivalent-payload→same result, same-key/different-payload→`idempotency_conflict`; TTL tiers per `D-7`. Implementing epics: Epic 1 + Epic 4.
- **Security** (`0005`): `S-4` layered authorization order (JWT → EventStore claim transform → tenant-access projection freshness → folder ACL → EventStore validators → Dapr deny-by-default + mTLS); `S-2` frozen production OIDC params; references-only credentials via Dapr secret store; `S-6`/`C9` sensitive-metadata classification; zero cross-tenant leakage; reserved `system` tenant rule. Implementing epics: Epic 2 + Epic 7 (7.1/7.2).
- **Observability** (`0006`): `I-6` OpenTelemetry OTLP, vendor-neutral pluggable exporters, metadata-only telemetry; `I-7` `/health/live` + `/health/ready`; `C2` freshness; the five Story 7.12 signals (projection lag, dead-letter depth, provider failures, stale locks, cleanup failures). Implementing story: Epic 7 (7.12).
- **Deployment** (`0007`): `I-2` one Docker image per service with Dapr sidecars; `I-3` production deny-by-default Dapr access control + mTLS (`dapr-policy-conformance` negative tests); `I-4` stable app IDs `eventstore`/`tenants`/`folders`/`folders-workers`/`folders-ui`; `I-1` Aspire local topology; state-store/pub-sub backends `D-2`-`D-5`. Implementing stories: Epic 7 (7.1/7.3).

### Runbook topic → existing evidence map (cross-link, do not duplicate)

| Runbook | Status | Cross-link these existing docs | Gap to author |
|---|---|---|---|
| `tenant-deletion.md` | exists | `c3-retention.md`, `retention-and-tenant-deletion.md` | none — preserve, index it |
| `retention.md` | new (thin) | `c3-retention.md`, `retention-and-tenant-deletion.md` | operator cleanup/compaction cadence; cross-link, no matrix copy |
| `alerts.md` | new | `incident-alerting-and-recovery.md`, `production-observability.md` | escalation/on-call checklist; mark live alert delivery `reference_pending` |
| `rollback.md` | new | `release-packages.md`, `container-images-and-dapr-app-ids.md` | **genuine gap**: revert package/image + post-rollback health verify |
| `provider-drift.md` | new | `scheduled-drift-and-policy-conformance.md`, `provider-integration-and-testing.md` | operator response to breaking/additive/unknown oasdiff drift |
| `reconciliation.md` | new | `canonical-error-catalog.md`, architecture C6 matrix | **genuine gap**: `unknown_provider_outcome`/`reconciliation_required` decision tree, no silent retry |
| `incident-mode.md` | new | `incident-alerting-and-recovery.md`, `operations-console.md` | **genuine gap**: `/_admin/incident-stream` use, disposition labels, handoff |

Key facts to preserve (from source, metadata-only): reconciliation error codes are `unknown_provider_outcome` and `reconciliation_required`; the incident-mode last-resort read path is `/_admin/incident-stream?folder={folderId}` with degraded-mode banner + operator-disposition labels + one-click copy of correlationId + UTC timestamp; tenant authority comes from auth context, never the query param; redaction stays enforced in incident mode.

### Previous Story Intelligence

- Story 7.16 proved the doc + static-conformance pattern works when inventories are re-derived from source and marker-bounded blocks are asserted exactly. Reuse its gate/test skeleton.
- Story 7.16 AC6/AC7 set the honesty precedent that is load-bearing here: reference-pending items (including the Story 7.17 live alerting/backup items) must stay visible with owner + gap + consuming story and must not be converted to covered by narrative. Carry that into the alerts/rollback runbooks.
- Story 7.15 review fixed a **File List omission for regenerated gate output** — list every modified gate report, workflow, and baseline filter (including the regenerated `_bmad-output/gates/baseline-ci/latest.json` if the baseline gate is run) in the Dev Agent Record File List.
- Story 7.14 review found a recursive-submodule negative control that bypassed the real guard helper — route every unsafe-command negative control through the same scanner the production checks use.
- Story 7.13 established lane separation: focused doc gates go into `contract-spine.yml` + the baseline Contracts.Tests filter only; do not create new workflow lanes.
- Story 7.16 grew from 14 to 16 `[Fact]`s during QA hardening and had to update `$runnerMethods` in lockstep. The vacuous-test guard compares `executedTests` to `$runnerMethods.Count`, so **every** `[Fact]` you add (including the AC10 negative controls) must also be added to `$runnerMethods` in the gate script, or the gate miscounts and fails closed.
- Story automator note: the 7.16 create-story child stalled in a cancelled parallel-tool loop after an invalid absolute path. Use repository-relative paths only when reading inputs in any sub-step.

### Project Structure Notes

- ADRs belong under `docs/adrs/` (numbered `NNNN-kebab-title.md`); the template is `docs/adrs/0000-template.md` and the only prior real ADR is `docs/adrs/0001-folder-domain-processor-persistence.md` (next number is `0002`). Per `project-context.md`, ADRs live under `docs/adrs/`; do not bury normative rules in ad hoc README text.
- Runbooks belong under `docs/runbooks/` (existing: `tenant-deletion.md`).
- Conformance test belongs under `tests/Hexalith.Folders.Contracts.Tests/Deployment/`.
- Gate script belongs under `tests/tools/`; gate report under `_bmad-output/gates/adr-runbook-docs/latest.json`.
- CI wiring belongs in `.github/workflows/contract-spine.yml` (after the NFR traceability step) and `tests/tools/run-baseline-ci-gates.ps1` (Contracts.Tests filter, line 31).
- Markdown is CRLF per `.editorconfig`; keep trimmed trailing whitespace and a final newline. The gate report and any YAML/shell artifacts stay LF. Build runs warnings-as-errors; the new test must be 0/0 warnings/errors and pass `dotnet format`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.17`] — Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md`] — Decision IDs `A-1`/`A-6`/`A-7`/`A-9`, `C0`/`C2`/`C9`/`C12`/`C13`, `D-7`, `S-2`/`S-4`/`S-6`, `I-1`-`I-7` and the C6 transition matrix the ADRs/runbooks cite.
- [Source: `docs/adrs/0000-template.md`] — Required ADR section structure and `non_policy_placeholder` contract.
- [Source: `docs/adrs/0001-folder-domain-processor-persistence.md`] — Worked ADR example (status, context, decision, consequences shape).
- [Source: `docs/runbooks/tenant-deletion.md`] — Existing runbook shape: disposition matrix, manual/automated steps, forbidden-evidence clause, gate command.
- [Source: `docs/exit-criteria/nfr-traceability.md`] — `operational-runbook-proof` evidence class and the NFR54/NFR55/NFR62-66 reference-pending gaps naming story `7-17`.
- [Source: `docs/exit-criteria/c3-retention.md`] — Retention policy source for the retention runbook cross-link.
- [Source: `docs/operations/incident-alerting-and-recovery.md`] — Incident-mode `/_admin/incident-stream` contract, five alert signals, backup/recovery boundary.
- [Source: `docs/operations/canonical-error-catalog.md`] — `unknown_provider_outcome` / `reconciliation_required` categories for the reconciliation runbook.
- [Source: `docs/operations/scheduled-drift-and-policy-conformance.md`] — Provider-drift detection workflow the drift runbook cross-links.
- [Source: `docs/operations/release-packages.md`] + [`docs/operations/container-images-and-dapr-app-ids.md`] — Release/image promotion sources for the rollback runbook.
- [Source: `tests/tools/run-provider-error-docs-gates.ps1`] + [`tests/tools/run-nfr-traceability-gates.ps1`] — Gate-script skeleton to copy.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs`] — Conformance-test skeleton (parsers, scanners, negative-control routing) to copy.
- [Source: `tests/tools/run-baseline-ci-gates.ps1#L31`] — Contracts.Tests filter append target.
- [Source: `.github/workflows/contract-spine.yml`] — Focused static-conformance lane; insert after `Run NFR traceability conformance gates`.
- [Source: `_bmad-output/implementation-artifacts/7-16-publish-nfr-traceability-bridge.md`] — Immediate predecessor; doc-gate pattern, honesty principle, and review learnings.

## Dev Agent Record

### Agent Model Used

Codex GPT-5 parent story-automator recovery after Claude Opus 4.8 dev-story attempts stalled.

### Debug Log References

- 2026-05-31: Claude Opus 4.8 create-story completed Story 7.17 and moved sprint-status to `ready-for-dev`; validation fixes narrowed the section contracts for the preserved `tenant-deletion.md`, ADR status/date parsing, metadata-only scope, and workflow filenames.
- 2026-05-31: Dev attempt 1 authored the ADRs, runbooks, and indexes, then stalled before static conformance tests/gates. Parent terminated the stale tmux session after source status remained `in-progress`.
- 2026-05-31: Dev attempt 2 validated the existing docs and read the conformance-test precedent, then stalled before writing test/gate artifacts. Parent completed the focused recovery implementation.
- 2026-05-31: `dotnet build Hexalith.Folders.slnx --no-restore -m:1` passed with 0 warnings/0 errors after adding `AdrRunbookDocsConformanceTests`.
- 2026-05-31: Focused `AdrRunbookDocsConformanceTests` passed 10/10; initial case-sensitive alert-runbook assertion was corrected to case-insensitive, then the focused test passed.
- 2026-05-31: `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1 -SkipRestoreBuild` passed 10/10 and regenerated `_bmad-output/gates/adr-runbook-docs/latest.json`.
- 2026-05-31: `dotnet format whitespace` + `dotnet format analyzers` with `--verify-no-changes` passed for the new test file.
- 2026-05-31: `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` passed; Contracts.Tests now runs 118 facts through the appended baseline filter.
- 2026-05-31: `git diff --check` passed; recursive-submodule scan over modified executable/docs surfaces found no `--recursive` setup command.
- 2026-05-31 (QA guardrail): Claude QA child stalled after loading the conformance test; parent completed the static automation audit. Hardened lane separation to assert release-package workflow/gate absence and strengthened runbook-index missing/orphan negative controls through exact-set comparisons. Re-ran build, focused test, focused gate, format/analyzers, baseline CI, diff hygiene, and recursive-submodule scan successfully. Wrote Story 7.17 QA summaries.

### Completion Notes List

- Published six retrospective ADRs (`0002`-`0007`) for contract, provider abstraction, idempotency, security, observability, and deployment decisions, each citing real architecture decision IDs and preserving the `0000` template plus existing `0001`.
- Published six new operator-facing runbooks (`retention`, `alerts`, `rollback`, `provider-drift`, `reconciliation`, `incident-mode`) and preserved `tenant-deletion.md` with its established section set. Alerts and rollback keep live alert delivery and backup/restore tooling as `reference_pending` rather than claiming live tooling exists.
- Added inventory-exact ADR and runbook indexes with marker-bounded tables.
- Added `AdrRunbookDocsConformanceTests` (10 facts) that re-derive the ADR/runbook inventory from the gate script manifest, parse real docs and indexes, validate real architecture decision citations, assert metadata-only posture and lane separation, and route AC10 negative controls through the same parsers/scanners.
- Added `run-adr-runbook-docs-gates.ps1` with the standard strict PowerShell gate posture, `utf8NoBOM` report writes, `$runnerMethods` lockstep guard, VSTest socket-denied xUnit fallback, bounded `surfaces[]`, and metadata-only `latest.json` evidence.
- Wired the focused gate into `contract-spine.yml` immediately after NFR traceability and appended the conformance FQN to the baseline Contracts.Tests filter. No PR `ci.yml`, scheduled drift, policy-conformance, release-package workflow, or release gate lane was broadened.
- QA guardrail hardening kept the fact count at 10 while adding release-lane exclusion assertions and parser-routed runbook-index missing/orphan negative controls. The gate script `$runnerMethods` stayed in exact lockstep with the `[Fact]` set.

### File List

- `_bmad-output/implementation-artifacts/7-17-publish-adr-set-and-maintenance-runbooks.md` (story tracking)
- `docs/adrs/0002-contract-spine-single-source-of-truth.md` (new)
- `docs/adrs/0003-provider-abstraction-and-capability-model.md` (new)
- `docs/adrs/0004-per-command-canonical-idempotency.md` (new)
- `docs/adrs/0005-layered-authorization-and-oidc.md` (new)
- `docs/adrs/0006-observability-and-operational-signals.md` (new)
- `docs/adrs/0007-container-deployment-with-dapr.md` (new)
- `docs/adrs/index.md` (new)
- `docs/runbooks/alerts.md` (new)
- `docs/runbooks/incident-mode.md` (new)
- `docs/runbooks/index.md` (new)
- `docs/runbooks/provider-drift.md` (new)
- `docs/runbooks/reconciliation.md` (new)
- `docs/runbooks/retention.md` (new)
- `docs/runbooks/rollback.md` (new)
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs` (new)
- `tests/tools/run-adr-runbook-docs-gates.ps1` (new)
- `_bmad-output/gates/adr-runbook-docs/latest.json` (new)
- `.github/workflows/contract-spine.yml` (modified)
- `tests/tools/run-baseline-ci-gates.ps1` (modified)
- `_bmad-output/gates/baseline-ci/latest.json` (regenerated)
- `_bmad-output/implementation-artifacts/tests/7-17-test-summary.md` (new)
- `_bmad-output/implementation-artifacts/tests/test-summary.md` (modified — latest QA summary now points to Story 7.17)

### Change Log

| Date       | Version | Description | Author |
| ---------- | ------- | ----------- | ------ |
| 2026-05-31 | 0.1     | Initial story context created. Status -> ready-for-dev. | Story Automator |
| 2026-05-31 | 1.0     | Published ADRs/runbooks/indexes, added 10-fact static conformance gate plus focused PowerShell runner/report, and wired contract-spine/baseline lanes. Build, focused test, focused gate, baseline gate, format checks, diff hygiene, and recursive-submodule scan pass. Status -> review. | Codex (parent recovery) |
| 2026-05-31 | 1.1     | QA guardrail hardened release-lane exclusion and runbook-index negative controls without changing the 10-fact count; regenerated QA summaries and reran all focused/baseline validations. | Story Automator QA |
| 2026-05-31 | 1.2     | Adversarial review (5-dimension fan-out + per-finding verification): 8 raw findings → 6 confirmed (3 MEDIUM, 3 LOW), 2 rejected. All 6 auto-fixed: incident-mode honesty overclaim, ADR 0004 hash-mechanism accuracy, two metadata-only scanner coverage gaps (provider/cloud credentials, Unix host roots) with fail-closed controls, fail-open ADR directory glob, and YAML-control intent. Build 0/0, focused gate 10/10, format/analyzers clean, diff/submodule hygiene clean. Status -> done. | Senior Developer Review (AI) |

## Senior Developer Review (AI)

**Reviewer:** jpiquot (automated story-automator review) · **Date:** 2026-05-31 · **Outcome:** Approved (auto-fix applied)

Empirical re-verification (not trust-the-claim): solution build 0 warnings / 0 errors; focused `AdrRunbookDocsConformanceTests` 10/10; baseline-ci gate `status: passed` with the appended Contracts.Tests filter; all 21 cited architecture decision IDs present in `architecture.md`; every ADR/runbook cross-link target resolves on disk; reconciliation codes `71`/`72` match `canonical-error-catalog.md`; conformance test is non-vacuous with negative controls routed through the real parsers/scanners; `$runnerMethods` in exact lockstep with the 10 `[Fact]` set; CI wiring placed immediately after the NFR step with no lane broadening; File List matches git reality.

A 5-dimension adversarial workflow (ADR claims, runbook content, test rigor, AC completeness, scope/metadata) produced 8 candidate findings; each was independently verified before counting. 6 confirmed and auto-fixed; 2 rejected as already-handled (report-skip idiom is suite-wide and the contract is hard-asserted elsewhere; the `Accepted` substring path has its own dedicated `Proposed`-status negative control).

Findings fixed:

1. **[MEDIUM honesty] `docs/runbooks/incident-mode.md:17`** — claimed `/_admin/incident-stream` is a "projection-independent authoritative event-stream read", the exact capability its own cross-linked source (`docs/operations/incident-alerting-and-recovery.md:33-34`) marks `reference_pending` / not shipped in MVP. Violated the story's load-bearing honesty rule (Dev Notes, mirrors 7.16 AC6). Reworded to describe the as-built degraded-projection-tolerant timeline read and mark the projection-independent reader `reference_pending`.
2. **[MEDIUM security] `AdrRunbookDocsConformanceTests.cs` `SecretMaterialPattern`** — metadata-only secret scanner missed GitHub PAT (`ghp_`/`github_pat_`), AWS `AKIA…`, and raw `Password=` forms; for a GitHub/Forgejo product these are the most likely future leak shapes. Broadened the alternation and added fail-closed negative controls.
3. **[MEDIUM security] `AdrRunbookDocsConformanceTests.cs` `HostAbsolutePathPattern`** — caught only `/home/` and `/Users/`, missing `/etc /var /opt /root /srv /mnt`. Broadened and added `/etc` + `/root` negative controls.
4. **[LOW accuracy] `docs/adrs/0004-…md:21`** — attributed NFC normalization and length-prefixing to `ComputeIdempotencyHash()`; the actual hasher (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs`) uses type-tagged delimiter-escaped encoding + ordinal ordering + duplicate-property rejection + SHA-256, and length-prefixing belongs to the separate server-internal `FolderCommandValidator.Fingerprint`. Corrected; NFC scoped to declared per-field normalization.
5. **[LOW soundness] `AdrRunbookDocsConformanceTests.cs:207`** — `000*.md` ADR glob would silently miss future `0010+` ADRs, a fail-open on AC7 "no missing index entry". Switched to `*.md` with `index.md` exclusion (mirrors the runbook branch).
6. **[LOW clarity] malformed-YAML negative control** — orphaned (no YAML production surface in this gate). Retained for AC8/AC10 literal compliance with an explanatory comment rather than removed.

All `[x]` task claims independently confirmed accurate. No CRITICAL issues at any point. No scope creep into the forbidden surfaces (NFR bridge, governance evidence, PR/drift/policy/release workflows, runtime/src). Re-validated after fixes: build, focused gate, whitespace, analyzers, diff hygiene, and recursive-submodule scan all clean.
