# Story 1.4: Author Phase 0.5 Pre-Spine Workshop deliverables

Status: ready-for-dev

Created: 2026-05-10

## Story

As an architect and maintainer,
I want Contract Spine blocking decisions resolved,
so that the canonical contract starts with real retention, input-limit, state, and auth values.

## Acceptance Criteria

1. Given the architecture exit-criteria plan, when the Pre-Spine Workshop deliverables are authored, then `docs/exit-criteria/c3-retention.md` exists and records concrete C3 retention durations per data class with owner, authority, value provenance, measurement/enforcement method, tenant-isolation implication, and dated decision metadata.
2. Given the architecture exit-criteria plan, when the Pre-Spine Workshop deliverables are authored, then `docs/exit-criteria/c4-input-limits.md` exists and records concrete C4 bounded MVP input limits with owner, authority, value provenance, measurement/enforcement method, tenant-isolation implication, and OpenAPI field-mapping notes for Phase 1 without creating or editing the Contract Spine.
3. Given the S-2 security decision in architecture, when the workshop deliverables are authored, then `docs/exit-criteria/s2-oidc-validation.md` documents OIDC validation parameters with issuer/audience pinning per environment, JWT validation rules, JWKS refresh intervals, claim provenance, explicit non-secret configuration placeholders, tenant-boundary implications, and no middleware/package/runtime integration changes.
4. Given the C6 architecture matrix, when the workshop deliverables are authored, then `docs/exit-criteria/c6-transition-matrix-mapping.md` documents a future implementation mapping plan that names the source architecture section, intended target implementation file, canonical rejection category, operator-disposition mapping, and required future aggregate-test coverage without creating `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` or aggregate tests.
5. Given Story 1.3 seeded templates and fixtures, when this story is complete, then deliverables reuse `docs/exit-criteria/_template.md` and existing fixture ownership notes without implementing Contract Spine endpoints, SDK generation, provider adapters, domain aggregates, CI workflow gates, authentication middleware, or production infrastructure.
6. Build or documentation verification for this story succeeds without provider credentials, tenant data, production secrets, running Dapr sidecars, Aspire topology, GitHub, Forgejo, generated SDKs, network access, or initialized nested submodules.
7. Given the four decision artifacts are authored, when they are reviewed, then each artifact includes `status`, `decision owner`, `approval authority`, `source inputs`, `last reviewed`, `open questions`, `Decision`, `Rationale`, `Verification impact`, and `Deferred implementation` sections so binding defaults, proposed workshop values, and unresolved human decisions are distinguishable.

## Tasks / Subtasks

- [ ] Inspect prior Phase 0 outputs before authoring deliverables. (AC: 5, 6)
  - [ ] Confirm Story 1.1 scaffold expectations and Story 1.2 root policy are available if verification depends on root docs or solution structure.
  - [ ] Confirm Story 1.3 has seeded `docs/exit-criteria/_template.md`, `docs/adrs/0000-template.md`, `tests/fixtures/previous-spine.yaml`, and fixture ownership notes, or record the exact missing prerequisite instead of broadening this story into fixture seeding.
  - [ ] Do not initialize or update nested submodules; use root-level submodules only as read-only references unless the user explicitly asks for nested submodules.
- [ ] Author `docs/exit-criteria/c3-retention.md`. (AC: 1, 5)
  - [ ] Start from `docs/exit-criteria/_template.md` if present; preserve the template's evidence and ownership fields.
  - [ ] Include retention durations for audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, and cleanup records.
  - [ ] For each data class, document retention duration, deletion/tombstone/anonymization behavior, cleanup trigger, operational evidence, and the downstream place where Phase 1 or later stories consume the value.
  - [ ] Include value provenance for every duration: source architecture section, Story 1.3 fixture/template, stakeholder workshop note, or explicit `needs human decision`.
  - [ ] State the tenant-isolation implication for each retained data class and avoid copying tenant payload values into examples.
  - [ ] Record decision owner `Tech Lead`, decision authority `Legal + PM`, Phase 1 entry deadline, and measurement method from the architecture operations plan.
  - [ ] Do not invent legal commitments without recording the source of the decision; if a stakeholder value is unavailable, record the blocking decision explicitly instead of writing a fake concrete value.
- [ ] Author `docs/exit-criteria/c4-input-limits.md`. (AC: 2, 5)
  - [ ] Start from `docs/exit-criteria/_template.md` if present.
  - [ ] Define max files, max bytes, max result count, max query duration, timeout behavior, truncation behavior, and included/excluded audit visibility for context-query families.
  - [ ] Map each limit to the Phase 1 Contract Spine fields that must receive the value, including `maxItems`, `maxLength`, `maxBytes`, `maxResultCount`, or equivalent OpenAPI extension metadata, without adding or editing OpenAPI files.
  - [ ] Include value provenance for every limit and state whether it is a binding MVP default, a proposed workshop value, or a `needs human decision` placeholder.
  - [ ] Record decision owner `Architect`, decision authority `PM`, Phase 1 entry deadline, and measurement method from the architecture operations plan.
  - [ ] Keep values bounded for MVP and note that later capacity calibration can revise them with evidence; do not replace C1/C5 release calibration work.
- [ ] Document S-2 OIDC validation parameters. (AC: 3)
  - [ ] Add the focused artifact `docs/exit-criteria/s2-oidc-validation.md`.
  - [ ] Include `ClockSkew = TimeSpan.FromSeconds(30)`, `RequireExpirationTime = true`, `RequireSignedTokens = true`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true`, and `ValidateIssuerSigningKey = true`.
  - [ ] Include JWKS retrieval behavior: `AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` and `RefreshInterval = TimeSpan.FromMinutes(1)` for forced refresh after signature-validation failure.
  - [ ] Document that issuer and audience are pinned per environment through syntactically valid non-secret configuration placeholders; do not commit real production issuer URLs, client secrets, provider credentials, or tenant-specific values.
  - [ ] Document claim provenance: `sub` is principal, `eventstore:tenant` is authoritative tenant after EventStore claim transformation, `eventstore:permission` gates command/query access, and tenant IDs in payloads are inputs to validate, not authority.
  - [ ] Keep the artifact documentation-only; do not add authentication middleware, package configuration, runtime integration, or environment-specific configuration files.
- [ ] Document C6 transition-matrix implementation mapping. (AC: 4)
  - [ ] Add the focused artifact `docs/exit-criteria/c6-transition-matrix-mapping.md`.
  - [ ] Reference `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)` as the source of truth.
  - [ ] Name the future target implementation path `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, state that later code must translate the architecture matrix 1:1, and do not create or modify that source file in this story.
  - [ ] Document that every unlisted `(state, event)` pair must reject with `state_transition_invalid`, keep state unchanged, and remain inspectable through idempotency record behavior.
  - [ ] Document that operator-disposition labels must come from the architecture state catalog and later UI mapping must be generated from or tested against that catalog.
  - [ ] Document the future aggregate-test requirement: every state and every event in the architecture vocabulary needs positive transition or explicit rejection coverage, without adding aggregate tests in this story.
- [ ] Add lightweight verification. (AC: 1, 2, 3, 4, 6)
  - [ ] Prefer a documentation test or script that checks the required files exist and contain no unresolved `TBD` placeholders for the Phase-1-blocking fields.
  - [ ] Check that no deliverable includes obvious secret values, provider tokens, production credentials, tenant data, raw file contents, or production-only URLs.
  - [ ] Check that verification evidence confirms no OpenAPI spine files, generated SDK outputs, REST/CLI/MCP/server code, authentication middleware, provider adapters, domain aggregates, aggregate tests, CI workflow gates, infrastructure files, or `FolderStateTransitions.cs` were added by this story.
  - [ ] Check each required artifact contains `status`, `decision owner`, `approval authority`, `source inputs`, `last reviewed`, `open questions`, `Decision`, `Rationale`, `Verification impact`, and `Deferred implementation` sections.
  - [ ] If an existing test project is available, integrate the check there; otherwise add the lightest local script or documented manual verification consistent with the current scaffold.
  - [ ] Run the relevant verification command and, when the scaffold supports it, `dotnet build Hexalith.Folders.slnx`.

## Dev Notes

### Scope Boundaries

- This story authors Phase 0.5 decision artifacts only. It does not implement the Contract Spine, OpenAPI operation inventory, NSwag SDK generation, idempotency hash helpers, parity oracle generation, provider adapters, domain aggregates, REST endpoints, CLI commands, MCP tools, UI components, workers, or CI workflow gates.
- C3 and C4 are Phase-1-blocking because Phase 1 OpenAPI schemas and idempotency TTL semantics consume their values. Do not start `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` in this story.
- C6 is already enumerated in the architecture. This story documents how implementation must map to it; it must not create `FolderStateTransitions.cs` or aggregate tests.
- S-2 validation parameters are already specified in architecture. This story records them in an implementation-ready artifact with environment placeholders; it must not wire authentication middleware.
- Every decision value must show provenance and approval state. If a value is not approved, record it as a proposed workshop value or `needs human decision`; do not present developer-authored estimates as binding stakeholder policy.
- Do not alter sibling submodules (`Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`). Use them only as read-only references.

### Required Deliverables

Expected new or updated files:

```text
docs/exit-criteria/c3-retention.md
docs/exit-criteria/c4-input-limits.md
docs/exit-criteria/s2-oidc-validation.md
docs/exit-criteria/c6-transition-matrix-mapping.md
```

Use these exact paths for the four decision artifacts. If an existing architecture-owned artifact appears to overlap, cross-link it from the required file instead of moving or renaming the deliverable.

### C3 Retention Content Requirements

- Cover audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, and cleanup records.
- Also account for folder metadata, soft-delete markers, auth claims copied into metadata, diagnostics, and rejected-command records when the workshop determines whether they are separate data classes or covered by the listed classes.
- Include duration, retention rationale, deletion or anonymization behavior, cleanup trigger, operational evidence, and downstream consumer.
- Record owner `Tech Lead`, authority `Legal + PM`, decision deadline `Phase 1 entry`, and measurement method `stakeholder workshop plus per-data-class table`.
- State how commit idempotency TTL inherits or references C3 where the architecture calls out D-7 commit TTL behavior.
- Avoid generic "retain as needed" language. If a value is not actually decided, mark the story blocked or record a deferred stakeholder decision rather than fabricating a number.

### C4 Input-Limit Content Requirements

- Cover maximum files, maximum bytes, maximum result count, maximum query duration, timeout behavior, truncation behavior, and audit visibility for included/excluded results.
- Identify which query families receive each bound: file tree, search, glob, metadata, bounded range, and context-query response payloads.
- Map values to the Phase 1 Contract Spine so the OpenAPI author can apply schema limits and extension metadata without reinterpretation.
- Mapping notes may name future OpenAPI schema fields, keywords, or extension metadata, but must not create or edit the OpenAPI spine in this story.
- Record owner `Architect`, authority `PM`, decision deadline `Phase 1 entry`, and measurement method `preliminary load probe plus product judgment`.
- Keep C4 separate from C1/C5 capacity calibration; this story defines MVP input bounds, not final scalability targets.

### S-2 OIDC Content Requirements

- Use `Microsoft.AspNetCore.Authentication.JwtBearer` validation semantics from architecture.
- Required validation settings: 30-second clock skew, expiration required, signed tokens required, issuer and audience validation enabled, lifetime validation enabled, issuer-signing-key validation enabled.
- Required JWKS behavior: 10-minute automatic refresh interval and 1-minute forced refresh minimum on signature-validation failure.
- Compatible provider language may include Keycloak, Microsoft Entra ID, Auth0, or any OIDC-compliant provider exposing `/.well-known/openid-configuration`.
- Non-pluggable claim rules: `sub` identifies principal; `eventstore:tenant` is authoritative tenant after EventStore transformation; `eventstore:permission` gates command/query access.
- Do not include real production issuer URLs, client secrets, private keys, tenant names, provider credentials, raw tokens, certificates, or environment-specific credentials. Example issuer, audience, JWKS, and client identifiers must be clearly non-production placeholders.

### C6 Mapping Content Requirements

- Source of truth: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`.
- Future implementation target: `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`; this story references the path only and must not create or modify it.
- State catalog has 11 states: `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`.
- Every unlisted `(state, event)` pair rejects with `state_transition_invalid`; CLI exit code is 74 and MCP failure kind is `state_transition_invalid`.
- Operator-disposition labels come from the architecture state catalog and later UI mapping must not invent independent labels.
- Future aggregate tests must cover every state and event in the architecture vocabulary with either the documented positive transition or explicit rejection; this story documents those coverage expectations only.

### Previous Story Intelligence

- Story 1.1 defines the scaffold boundaries, dependency direction, `net10.0` target, central package management, and no-secret build constraints.
- Story 1.2 hardens root configuration and submodule policy. Keep root-level submodule initialization only; never use `git submodule update --init --recursive` by default.
- Story 1.3 seeds the fixture and exit-criteria template locations. This story should build on those locations rather than reseeding or replacing them wholesale.
- Recent story commits (`6771d62`, `b6b4eef`) show the create-story flow keeps artifacts ready-for-dev and lets later `dev-story` implementation perform file changes. Preserve that separation.

### Project Context Notes

- Target .NET remains `net10.0`; nullable, implicit usings, `LangVersion=latest`, and warnings-as-errors are inherited from root build configuration.
- Contracts remain behavior-free. Decision documents may reference future Contract Spine fields, but must not add behavior to `Hexalith.Folders.Contracts`.
- Authoritative tenant context comes from authentication context and EventStore envelopes, never request payload authority.
- Do not store raw provider tokens, secrets, file contents, diffs, generated context payloads, or unauthorized resource existence in events, logs, traces, metrics, projections, audit records, diagnostics, errors, or documentation examples.

### Testing Guidance

- A focused documentation verification is sufficient for this story if the scaffold is not ready for full test integration.
- Verification should prove the required artifacts exist, required ownership metadata is present, Phase-1-blocking values are not left as unresolved placeholders, and no obvious secret material is committed.
- Verification should prove each artifact distinguishes binding MVP defaults, proposed workshop values, and unresolved human decisions through provenance and approval metadata.
- Verification should prove this story did not add implementation, generated contract/client, CI, infrastructure, middleware, aggregate, or aggregate-test files outside the expected documentation/verification surface.
- Build verification should use `dotnet build Hexalith.Folders.slnx` when the scaffold supports it. If build is blocked by incomplete prior scaffold work, record the exact prerequisite instead of expanding this story's scope.
- Tests and scripts must not require Dapr sidecars, Aspire topology, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, or initialized nested submodules.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.4: Author Phase 0.5 Pre-Spine Workshop deliverables`
- `_bmad-output/planning-artifacts/epics.md#Pre-Spine Workshop (Phase 0.5 - exit criteria deliverables)`
- `_bmad-output/planning-artifacts/architecture.md#Architecture Exit Criteria - Targets to Resolve`
- `_bmad-output/planning-artifacts/architecture.md#Exit Criteria Operations Plan`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`
- `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Handoff`
- `_bmad-output/planning-artifacts/prd.md#Deferred Quantitative Targets - Architecture Exit Criteria`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/prd.md#Integration and Contract Compatibility`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`
- `CLAUDE.md#Git Submodules`

## Project Structure Notes

- The expected deliverables live under `docs/exit-criteria/`, reusing the Story 1.3 template area.
- If `docs/exit-criteria/_template.md` is still missing when development begins, treat that as a prerequisite drift from Story 1.3 and record it before creating a local substitute.
- This story intentionally produces documentation artifacts and lightweight verification only; source code changes belong to later stories unless needed for a documentation test harness already established by the scaffold.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-10 | Party-mode review applied documentation-only scope, provenance, approval-state, and verification guardrails. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date: 2026-05-10T19:08:42Z
- Selected story key: `1-4-author-phase-0-5-pre-spine-workshop-deliverables`
- Command/skill invocation used: `/bmad-party-mode 1-4-author-phase-0-5-pre-spine-workshop-deliverables; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Paige (Technical Writer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - Clarify that all four deliverables are documentation-only decision artifacts, not implementation work.
  - Require exact artifact paths, decision metadata, source provenance, and explicit approval state for values.
  - Tighten C3/C4/S-2/C6 wording so developers do not invent stakeholder policy or accidentally create OpenAPI, middleware, aggregate, test, CI, or infrastructure artifacts.
  - Strengthen offline verification evidence for required sections, no unresolved placeholders, no secret material, no tenant payload examples, and no scope leakage.
- Changes applied:
  - Added acceptance criteria for fixed artifact paths, provenance, tenant-isolation implications, documentation-only scope, and required decision sections.
  - Updated tasks and Dev Notes to require binding/proposed/unresolved value classification and source inputs.
  - Added negative-scope verification for generated contract/client files, middleware, aggregates, aggregate tests, CI gates, infrastructure, and `FolderStateTransitions.cs`.
  - Tightened S-2 placeholder and secret-exclusion language.
- Findings deferred:
  - Final C3 retention durations require Legal + PM authority when not already approved.
  - Final C4 bounded MVP input limits require PM/API-owner approval when not already approved.
  - OIDC issuer/audience/JWKS policy values and C6 operator-disposition/rejection semantics must retain named security/domain authority provenance in the deliverables.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
