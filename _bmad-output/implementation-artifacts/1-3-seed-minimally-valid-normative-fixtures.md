# Story 1.3: Seed minimally valid normative fixtures

Status: review

Created: 2026-05-10

## Story

As a maintainer,
I want normative fixtures and artifact templates to be minimally valid and owned by later gates,
so that contract, parity, redaction, encoding, and load tests have stable inputs rather than empty placeholders.

## Acceptance Criteria

1. Given the scaffolded repository, when fixture placeholders are seeded, then `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, and `tests/fixtures/idempotency-encoding-corpus.json` exist with minimal valid content.
2. Fixture schemas or smoke validation prove the files are parseable and intentionally incomplete where later stories own final semantics.
3. `tests/load`, `tests/tools/parity-oracle-generator`, `docs/exit-criteria/_template.md`, and `docs/adrs/0000-template.md` exist with ownership notes linking them to later CI or release-readiness stories.
4. The seeded fixtures do not contain real tenant data, provider tokens, credential material, file contents, repository secrets, or production URLs.
5. Build or test verification for this story succeeds without provider credentials, tenant data, running Dapr sidecars, Aspire topology, GitHub, Forgejo, or initialized nested submodules.
6. Each fixture or template includes stable, machine-checkable ownership metadata identifying its owning future workstream, intended future test use, known omissions, mutation rules, and whether each value set is a non-policy placeholder or an intentionally synthetic sentinel.

## Tasks / Subtasks

- [x] Inspect scaffold and root-policy outputs before changing fixture files. (AC: 1, 3, 5)
  - [x] Confirm Story 1.1 has created the expected `tests/`, `docs/`, and solution/test project structure, or record the exact missing scaffold prerequisite instead of broadening this story into scaffold work.
  - [x] Confirm Story 1.2 root configuration and submodule policy are present if this story depends on build or documentation checks created there.
  - [x] Do not initialize or update nested submodules; root-level submodules are read-only references unless the user explicitly asks otherwise.
- [x] Seed `tests/fixtures/audit-leakage-corpus.json`. (AC: 1, 2, 4)
  - [x] Use valid JSON with a schema/version marker, intent/ownership note, and a small sentinel set covering secret-shaped strings, credential-shaped values, file-path metadata, branch names, commit-message metadata, and provider diagnostic payload metadata.
  - [x] Mark the corpus intentionally minimal and owned by later sentinel-redaction CI work; do not claim exhaustive security coverage in this story.
  - [x] Keep every value synthetic, metadata-only, and obviously non-secret; avoid real tenant, user, organization, repository, provider, file-path, URL, email, customer-derived, or production-looking values.
  - [x] Tag intentionally secret-shaped samples with explicit synthetic-sentinel metadata so the story's banned-content check can distinguish allowed sentinel fixtures from accidental real credentials.
- [x] Seed `tests/fixtures/parity-contract.schema.json`. (AC: 1, 2)
  - [x] Use valid JSON Schema, preferably draft 2020-12, with the minimum row shape needed for later generated parity-oracle validation.
  - [x] Include placeholders for transport-parity dimensions such as auth outcome, error code set, idempotency key rule, audit metadata keys, correlation field path, and terminal states.
  - [x] Include placeholders for behavioral-parity dimensions such as pre-SDK error class, idempotency-key sourcing, correlation-ID sourcing, CLI exit code, and MCP failure kind.
  - [x] Define only a fixture/oracle input shape, not the public product contract shape; do not implement the parity oracle generator or complete operation inventory in this story.
- [x] Seed `tests/fixtures/previous-spine.yaml`. (AC: 1, 2)
  - [x] Use valid YAML with a version/source marker and an intentionally empty or minimal operation list.
  - [x] Document that Phase 1 Contract Spine work replaces this seed with a copy of the v1 spine for symmetric drift detection.
  - [x] Treat this as a synthetic placeholder fixture, not an OpenAPI document or partial Contract Spine; do not add an `openapi:` root key.
  - [x] Do not author `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; Contract Spine authoring belongs to Story 1.6 and later Phase 1 stories.
- [x] Seed `tests/fixtures/idempotency-encoding-corpus.json`. (AC: 1, 2, 4)
  - [x] Use valid JSON with synthetic canonicalization cases for NFC, NFD, NFKC, NFKD, zero-width-joiner, casing, and ULID casing inputs.
  - [x] Record expected intent at a high level only if the canonical hash algorithm is not yet implemented; later idempotency stories own final equivalence assertions.
  - [x] Keep sample payloads metadata-only and free of file contents or secrets.
  - [x] Include code-point labels or escaped Unicode forms for invisible and normalization-sensitive samples so future maintainers can review the corpus without guessing which characters are present.
- [x] Add ownership notes for deferred artifact areas. (AC: 3)
  - [x] Ensure `tests/load/` contains a README or minimal project note identifying capacity-smoke and release calibration ownership, non-goals, and mutation rules.
  - [x] Ensure `tests/tools/parity-oracle-generator/` contains a README or placeholder note identifying Phase 1 Contract Spine and parity-oracle generator ownership, non-goals, and mutation rules.
  - [x] Ensure `docs/exit-criteria/_template.md` exists and names required fields for C1-C13 evidence without filling policy decisions prematurely; mark unresolved policy fields as placeholders.
  - [x] Ensure `docs/adrs/0000-template.md` exists and is a reusable ADR template, not a completed ADR or architecture decision.
  - [x] Use a consistent ownership metadata shape where practical: `owner_workstream`, `future_test_use`, `known_omissions`, `mutation_rules`, `non_policy_placeholder`, and `synthetic_data_only`.
- [x] Add parseability and ownership verification. (AC: 2, 5)
  - [x] Prefer a small test in the existing scaffold test project that parses all JSON/YAML fixture files and asserts ownership notes exist for deferred artifact areas.
  - [x] Verify JSON fixtures load as JSON and YAML fixtures load as YAML through existing repository libraries or the lightest available scaffold mechanism; do not add a new parser package unless it is already centrally available.
  - [x] Add a banned-content check for obvious secrets, provider tokens, connection strings, production URLs, local absolute paths, real tenant/provider identifiers, file contents, diffs, generated context payloads, and customer-derived values.
  - [x] Make the banned-content check fail closed for untagged secret-shaped strings while allowing only explicitly tagged synthetic sentinel samples in `audit-leakage-corpus.json`.
  - [x] If Story 1.1 has not provided a usable test project yet, add the lightest local script or documentation check that fits the scaffold without creating a parallel test framework.
  - [x] Verify `dotnet build Hexalith.Folders.slnx` when the scaffold supports it; otherwise record the exact scaffold prerequisite that blocks build verification.

## Dev Notes

### Scope Boundaries

- This story seeds minimally valid normative fixtures and ownership notes only.
- Do not implement Contract Spine endpoints, OpenAPI operation inventory, NSwag generation, parity-oracle generation, sentinel-redaction pipeline, provider adapters, lifecycle domain logic, load tests, CI workflows, CLI commands, MCP tools, or UI pages.
- Do not turn placeholder artifacts into final policy decisions. C3 retention, C4 input limits, C6 state-transition implementation, C9 sensitive-metadata classification, and C13 parity semantics are owned by later stories and release-readiness work.
- Do not modify sibling submodules (`Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`). Use them only as read-only references.
- Minimal valid means the smallest synthetic metadata-only artifact that parses successfully and demonstrates required structural fields without asserting final policy behavior.
- This story does not define Contract Spine OpenAPI, parity oracle behavior, redaction pipeline behavior, load thresholds, provider-specific behavior, or final policy semantics.

### Required Fixture Outputs

Expected files for this story:

```text
tests/fixtures/audit-leakage-corpus.json
tests/fixtures/parity-contract.schema.json
tests/fixtures/previous-spine.yaml
tests/fixtures/idempotency-encoding-corpus.json
tests/load/README.md
tests/tools/parity-oracle-generator/README.md
docs/exit-criteria/_template.md
docs/adrs/0000-template.md
```

If Story 1.1 already created one or more of these files as empty placeholders, replace the empty placeholder with minimal valid content. If it already contains stronger valid content, preserve it and only add missing ownership or parseability coverage.

### Minimal Content Requirements

- `audit-leakage-corpus.json` should be a metadata-only sentinel corpus. It should contain synthetic samples that later redaction tests can iterate without exposing real data.
- `parity-contract.schema.json` should validate a future `parity-contract.yaml` row shape after conversion or fixture loading. Keep it small, but include both transport and behavioral parity categories so later stories do not erase CLI/MCP behavior behind the SDK wrapper.
- `previous-spine.yaml` is a seed for symmetric drift detection. It is not the Contract Spine, should not contain an `openapi:` root, and should not be treated as source of truth for public API behavior.
- `idempotency-encoding-corpus.json` should preserve tricky Unicode and casing cases that later `ComputeIdempotencyHash()` work must consume.
- Ownership notes must make the future owner clear: Phase 1 Contract Spine and parity stories own parity fixtures, redaction/audit stories own leakage expansion, idempotency stories own canonical hash expectations, capacity/release stories own load evidence, and architecture/release governance owns exit criteria and ADRs. Each note should state owner, purpose, known omissions, non-goals, and mutation rules.
- Secret-shaped sentinel values must be obviously synthetic, tagged as intentional sentinels, and excluded from any claim that the repository contains real secrets or production data.
- Invisible or normalization-sensitive Unicode samples should be paired with labels or escaped code-point descriptions so reviewers and future tests can distinguish fixture intent from accidental editor corruption.
- Template files should use placeholder markers for unresolved evidence or decision fields; they must not read as completed release evidence, final policy, or an accepted architecture decision.

### Previous Story Intelligence

Story 1.1 defines the scaffold shape and intentionally empty placeholders. Story 1.3 should harden those placeholders without expanding into scaffold creation.

Story 1.2 defines root reproducibility and submodule policy. Keep the same guardrails:

- Initialize/update only root-level submodules by default.
- Never use `git submodule update --init --recursive` or equivalent recursive nested-submodule commands unless explicitly requested.
- Build and fixture validation must not require nested submodule state, provider credentials, tenant seed data, production secrets, Dapr sidecars, Keycloak, Redis, GitHub, or Forgejo.

### Testing Guidance

- Use the existing test framework from the scaffold when available. A focused fixture smoke test is enough for this story.
- Tests should parse JSON and YAML with the same libraries already present in the repo or standard .NET tooling already referenced centrally. Avoid adding a new parser package only for this story unless the scaffold already depends on it.
- Validate that required ownership notes exist, but avoid brittle assertions on full prose.
- Security checks should verify no obvious real secret markers are introduced, while leaving comprehensive sentinel scanning to later redaction pipeline stories. The check should include a narrow allow-list for intentionally tagged synthetic sentinel samples so it catches accidental secrets without blocking the required leakage corpus.
- Parseability and safety tests should read fixture files directly and must not start application hosts, Aspire, Dapr, Redis, Keycloak, Testcontainers, GitHub, Forgejo, provider SDK authentication, external network calls, or submodule initialization.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.3: Seed minimally valid normative fixtures`
- `_bmad-output/planning-artifacts/epics.md#Solution Scaffolding (Phase 0 - sibling-module starter pattern)`
- `_bmad-output/planning-artifacts/epics.md#Pre-Spine Workshop (Phase 0.5 - exit criteria deliverables)`
- `_bmad-output/planning-artifacts/architecture.md#Architecture Exit Criteria - Targets to Resolve`
- `_bmad-output/planning-artifacts/architecture.md#File Organization Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Handoff`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/prd.md#Integration and Contract Compatibility`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `AGENTS.md#Git Submodules`
- `CLAUDE.md#Git Submodules`

## Project Structure Notes

- The story assumes the scaffolded `tests/fixtures`, `tests/load`, `tests/tools`, `docs/exit-criteria`, and `docs/adrs` paths either exist from Story 1.1 or are created here only as narrowly scoped fixture/template locations.
- There is no discoverable `project-context.md`; use planning artifacts and previous story files as implementation context.
- Fixture names are architecture-owned and should not be renamed without updating the architecture and downstream story references.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Implemented minimally valid normative fixtures, deferred artifact ownership notes, and parseability/synthetic-data smoke tests. | Codex |
| 2026-05-10 | Applied `bmad-advanced-elicitation` hardening for synthetic sentinel tagging, machine-checkable ownership metadata, Unicode fixture reviewability, and banned-content false-positive boundaries. | Codex |
| 2026-05-10 | Party-mode review applied fixture validity, banned-content, non-operative placeholder, and offline verification clarifications. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5

### Debug Log References

- 2026-05-11: `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore` failed in red phase against placeholder fixtures.
- 2026-05-11: `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore` passed after fixture and template seeding.
- 2026-05-11: `dotnet build Hexalith.Folders.slnx --no-restore` passed with 0 warnings and 0 errors.
- 2026-05-11: `dotnet test Hexalith.Folders.slnx --no-build --no-restore` passed.

### Completion Notes List

- Confirmed Story 1.1 scaffold outputs and Story 1.2 root/submodule policy were already present; no nested submodules were initialized or updated.
- Seeded the required normative fixtures with parseable, intentionally minimal, synthetic metadata-only content and machine-checkable ownership metadata.
- Added deferred artifact ownership notes for load tests, parity oracle generator, exit criteria, and ADR template areas without implementing final policy semantics or runnable generators/load infrastructure.
- Added `FixtureContractTests` coverage for JSON/YAML parseability, ownership metadata, synthetic sentinel tagging, and banned-content guardrails.

### File List

- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/adrs/0000-template.md`
- `docs/exit-criteria/_template.md`
- `tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/parity-contract.schema.json`
- `tests/fixtures/previous-spine.yaml`
- `tests/load/README.md`
- `tests/tools/parity-oracle-generator/README.md`

## Party-Mode Review

- Date/time: 2026-05-10T18:00:47Z
- Selected story key: `1-3-seed-minimally-valid-normative-fixtures`
- Command/skill invocation used: `/bmad-party-mode 1-3-seed-minimally-valid-normative-fixtures; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Clarify that "minimal valid" means parseable, synthetic, metadata-only structure without final policy semantics.
  - Tighten `previous-spine.yaml` so it remains a synthetic placeholder and cannot become a partial Contract Spine OpenAPI artifact.
  - Tighten `parity-contract.schema.json` so it defines fixture/oracle input shape only, not product contract shape or parity behavior.
  - Add explicit banned-content and offline verification boundaries for fixture smoke tests.
  - Clarify ownership notes for deferred load, parity, exit-criteria, and ADR artifacts.
- Changes applied:
  - Added AC6 for fixture/template ownership metadata.
  - Added synthetic-only hygiene requirements and banned-content verification guidance.
  - Added non-goal language for Contract Spine, parity oracle, redaction pipeline, load thresholds, provider behavior, and final policy semantics.
  - Added parseability and direct-file verification guidance that avoids app hosts, external services, network calls, and nested submodule initialization.
  - Added change-log evidence for this party-mode review.
- Findings deferred:
  - Final Contract Spine OpenAPI shape remains Story 1.6.
  - Actual parity oracle generation and comparison semantics remain deferred.
  - Redaction policy behavior and enforcement pipeline remain deferred.
  - Load-test thresholds, environments, and provider-specific scenarios remain deferred.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-10T23:03:07Z
- Selected story key: `1-3-seed-minimally-valid-normative-fixtures`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-3-seed-minimally-valid-normative-fixtures`
- Batch 1 method names: Security Audit Personas; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Expert Panel Review; Architecture Decision Records; Comparative Analysis Matrix; Occam's Razor Application; First Principles Analysis
- Findings summary:
  - Secret-shaped fixture values need explicit synthetic-sentinel metadata so safety checks do not either miss accidental credentials or reject the intended leakage corpus.
  - Ownership metadata needs stable fields that a focused smoke test can verify without brittle prose matching.
  - Unicode normalization fixtures need visible labels or escaped code-point descriptions because invisible characters are easy to corrupt during review.
  - Template artifacts need placeholder markers so they do not look like completed release evidence or accepted ADR decisions.
- Changes applied:
  - Tightened AC6 around machine-checkable ownership metadata and synthetic sentinel classification.
  - Added tasks for sentinel tagging, Unicode/code-point labeling, consistent ownership fields, and fail-closed banned-content validation.
  - Added minimal-content guidance for synthetic sentinels, normalization-sensitive samples, and template placeholders.
  - Clarified test guidance so intentionally tagged sentinels are allowed while untagged secret-shaped strings still fail.
- Findings deferred:
  - Final redaction taxonomy and enforcement pipeline remain owned by later redaction/audit stories.
  - Final idempotency hash equivalence assertions remain owned by later idempotency stories.
  - Final Contract Spine and parity oracle semantics remain owned by Story 1.6 and parity stories.
  - Exact smoke-test implementation mechanism remains open to the dev-story agent.
- Final recommendation: ready-for-dev
