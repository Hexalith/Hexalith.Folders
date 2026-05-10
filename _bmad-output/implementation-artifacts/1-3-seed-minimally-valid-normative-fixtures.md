# Story 1.3: Seed minimally valid normative fixtures

Status: ready-for-dev

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

## Tasks / Subtasks

- [ ] Inspect scaffold and root-policy outputs before changing fixture files. (AC: 1, 3, 5)
  - [ ] Confirm Story 1.1 has created the expected `tests/`, `docs/`, and solution/test project structure, or record the exact missing scaffold prerequisite instead of broadening this story into scaffold work.
  - [ ] Confirm Story 1.2 root configuration and submodule policy are present if this story depends on build or documentation checks created there.
  - [ ] Do not initialize or update nested submodules; root-level submodules are read-only references unless the user explicitly asks otherwise.
- [ ] Seed `tests/fixtures/audit-leakage-corpus.json`. (AC: 1, 2, 4)
  - [ ] Use valid JSON with a schema/version marker, intent/ownership note, and a small sentinel set covering secret-shaped strings, credential-shaped values, file-path metadata, branch names, commit-message metadata, and provider diagnostic payload metadata.
  - [ ] Mark the corpus intentionally minimal and owned by later sentinel-redaction CI work; do not claim exhaustive security coverage in this story.
  - [ ] Keep every value synthetic and obviously non-secret.
- [ ] Seed `tests/fixtures/parity-contract.schema.json`. (AC: 1, 2)
  - [ ] Use valid JSON Schema, preferably draft 2020-12, with the minimum row shape needed for later generated parity-oracle validation.
  - [ ] Include placeholders for transport-parity dimensions such as auth outcome, error code set, idempotency key rule, audit metadata keys, correlation field path, and terminal states.
  - [ ] Include placeholders for behavioral-parity dimensions such as pre-SDK error class, idempotency-key sourcing, correlation-ID sourcing, CLI exit code, and MCP failure kind.
  - [ ] Do not implement the parity oracle generator or complete operation inventory in this story.
- [ ] Seed `tests/fixtures/previous-spine.yaml`. (AC: 1, 2)
  - [ ] Use valid YAML with a version/source marker and an intentionally empty or minimal operation list.
  - [ ] Document that Phase 1 Contract Spine work replaces this seed with a copy of the v1 spine for symmetric drift detection.
  - [ ] Do not author `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; Contract Spine authoring belongs to Story 1.6 and later Phase 1 stories.
- [ ] Seed `tests/fixtures/idempotency-encoding-corpus.json`. (AC: 1, 2, 4)
  - [ ] Use valid JSON with synthetic canonicalization cases for NFC, NFD, NFKC, NFKD, zero-width-joiner, casing, and ULID casing inputs.
  - [ ] Record expected intent at a high level only if the canonical hash algorithm is not yet implemented; later idempotency stories own final equivalence assertions.
  - [ ] Keep sample payloads metadata-only and free of file contents or secrets.
- [ ] Add ownership notes for deferred artifact areas. (AC: 3)
  - [ ] Ensure `tests/load/` contains a README or minimal project note identifying capacity-smoke and release calibration ownership.
  - [ ] Ensure `tests/tools/parity-oracle-generator/` contains a README or placeholder note identifying Phase 1 Contract Spine and parity-oracle generator ownership.
  - [ ] Ensure `docs/exit-criteria/_template.md` exists and names required fields for C1-C13 evidence without filling policy decisions prematurely.
  - [ ] Ensure `docs/adrs/0000-template.md` exists and is a reusable ADR template, not a completed ADR.
- [ ] Add parseability and ownership verification. (AC: 2, 5)
  - [ ] Prefer a small test in the existing scaffold test project that parses all JSON/YAML fixture files and asserts ownership notes exist for deferred artifact areas.
  - [ ] If Story 1.1 has not provided a usable test project yet, add the lightest local script or documentation check that fits the scaffold without creating a parallel test framework.
  - [ ] Verify `dotnet build Hexalith.Folders.slnx` when the scaffold supports it; otherwise record the exact scaffold prerequisite that blocks build verification.

## Dev Notes

### Scope Boundaries

- This story seeds minimally valid normative fixtures and ownership notes only.
- Do not implement Contract Spine endpoints, OpenAPI operation inventory, NSwag generation, parity-oracle generation, sentinel-redaction pipeline, provider adapters, lifecycle domain logic, load tests, CI workflows, CLI commands, MCP tools, or UI pages.
- Do not turn placeholder artifacts into final policy decisions. C3 retention, C4 input limits, C6 state-transition implementation, C9 sensitive-metadata classification, and C13 parity semantics are owned by later stories and release-readiness work.
- Do not modify sibling submodules (`Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`). Use them only as read-only references.

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
- `previous-spine.yaml` is a seed for symmetric drift detection. It is not the Contract Spine and should not be treated as source of truth for public API behavior.
- `idempotency-encoding-corpus.json` should preserve tricky Unicode and casing cases that later `ComputeIdempotencyHash()` work must consume.
- Ownership notes must make the future owner clear: Phase 1 Contract Spine and parity stories own parity fixtures, redaction/audit stories own leakage expansion, idempotency stories own canonical hash expectations, capacity/release stories own load evidence, and architecture/release governance owns exit criteria and ADRs.

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
- Security checks should verify no obvious real secret markers are introduced, while leaving comprehensive sentinel scanning to later redaction pipeline stories.

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
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
