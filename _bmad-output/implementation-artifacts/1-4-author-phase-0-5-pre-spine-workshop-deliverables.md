# Story 1.4: Author Phase 0.5 Pre-Spine Workshop deliverables

Status: done

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
8. Given a Phase-1-blocking value is not approved by its named authority, when the artifact is authored, then the value is marked `needs human decision`, the artifact status is not `approved`, and the story completion notes identify the blocker rather than presenting the story as fully ready for Contract Spine authoring.
9. Given verification is run, when the decision artifacts are checked, then every C3/C4 table row and S-2/C6 mapping entry has explicit provenance, approval state, consuming future artifact, and review date, and verification fails on generic placeholders such as `TBD`, `retain as needed`, production-looking issuer URLs, secret-looking values, or undocumented numeric limits.

## Tasks / Subtasks

- [x] Inspect prior Phase 0 outputs before authoring deliverables. (AC: 5, 6)
  - [x] Confirm Story 1.1 scaffold expectations and Story 1.2 root policy are available if verification depends on root docs or solution structure.
  - [x] Confirm Story 1.3 has seeded `docs/exit-criteria/_template.md`, `docs/adrs/0000-template.md`, `tests/fixtures/previous-spine.yaml`, and fixture ownership notes, or record the exact missing prerequisite instead of broadening this story into fixture seeding.
  - [x] Do not initialize or update nested submodules; use root-level submodules only as read-only references unless the user explicitly asks for nested submodules.
- [x] Author `docs/exit-criteria/c3-retention.md`. (AC: 1, 5)
  - [x] Start from `docs/exit-criteria/_template.md` if present; preserve the template's evidence and ownership fields.
  - [x] Include retention durations for audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, and cleanup records.
  - [x] For each data class, document retention duration, deletion/tombstone/anonymization behavior, cleanup trigger, operational evidence, and the downstream place where Phase 1 or later stories consume the value.
  - [x] Include value provenance for every duration: source architecture section, Story 1.3 fixture/template, stakeholder workshop note, or explicit `needs human decision`.
  - [x] State the tenant-isolation implication for each retained data class and avoid copying tenant payload values into examples.
  - [x] Record decision owner `Tech Lead`, decision authority `Legal + PM`, Phase 1 entry deadline, and measurement method from the architecture operations plan.
  - [x] Classify every duration as `approved`, `proposed workshop value`, or `needs human decision`; do not mix those states in one row without explaining which downstream consumer is blocked.
  - [x] Include a commit-idempotency TTL row or note showing how D-7 `commit = retention-period(C3)` inherits from the approved retention decision, or mark it as the explicit Phase 1 blocker.
  - [x] Do not invent legal commitments without recording the source of the decision; if a stakeholder value is unavailable, record the blocking decision explicitly instead of writing a fake concrete value.
- [x] Author `docs/exit-criteria/c4-input-limits.md`. (AC: 2, 5)
  - [x] Start from `docs/exit-criteria/_template.md` if present.
  - [x] Define max files, max bytes, max result count, max query duration, timeout behavior, truncation behavior, and included/excluded audit visibility for context-query families.
  - [x] Map each limit to the Phase 1 Contract Spine fields that must receive the value, including `maxItems`, `maxLength`, `maxBytes`, `maxResultCount`, or equivalent OpenAPI extension metadata, without adding or editing OpenAPI files.
  - [x] Include value provenance for every limit and state whether it is a binding MVP default, a proposed workshop value, or a `needs human decision` placeholder.
  - [x] Define units and boundary semantics for every numeric limit: whether the bound is inclusive, whether it applies before or after authorization/path filtering, and whether timeout/truncation returns partial results or a canonical error.
  - [x] State whether each limit is global for MVP or tenant-tunable later; do not introduce tenant-specific configuration files or per-tenant override behavior in this story.
  - [x] Record decision owner `Architect`, decision authority `PM`, Phase 1 entry deadline, and measurement method from the architecture operations plan.
  - [x] Keep values bounded for MVP and note that later capacity calibration can revise them with evidence; do not replace C1/C5 release calibration work.
- [x] Document S-2 OIDC validation parameters. (AC: 3)
  - [x] Add the focused artifact `docs/exit-criteria/s2-oidc-validation.md`.
  - [x] Include `ClockSkew = TimeSpan.FromSeconds(30)`, `RequireExpirationTime = true`, `RequireSignedTokens = true`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true`, and `ValidateIssuerSigningKey = true`.
  - [x] Include JWKS retrieval behavior: `AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` and `RefreshInterval = TimeSpan.FromMinutes(1)` for forced refresh after signature-validation failure.
  - [x] Document that issuer and audience are pinned per environment through syntactically valid non-secret configuration placeholders; do not commit real production issuer URLs, client secrets, provider credentials, or tenant-specific values.
  - [x] Use clearly non-production examples such as `.invalid` issuer hosts and placeholder audience names; verification must reject realistic production domains, raw JWTs, client secrets, private keys, certificates, or tenant identifiers.
  - [x] Document claim provenance: `sub` is principal, `eventstore:tenant` is authoritative tenant after EventStore claim transformation, `eventstore:permission` gates command/query access, and tenant IDs in payloads are inputs to validate, not authority.
  - [x] Keep the artifact documentation-only; do not add authentication middleware, package configuration, runtime integration, or environment-specific configuration files.
- [x] Document C6 transition-matrix implementation mapping. (AC: 4)
  - [x] Add the focused artifact `docs/exit-criteria/c6-transition-matrix-mapping.md`.
  - [x] Reference `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)` as the source of truth.
  - [x] Name the future target implementation path `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, state that later code must translate the architecture matrix 1:1, and do not create or modify that source file in this story.
  - [x] Document that every unlisted `(state, event)` pair must reject with `state_transition_invalid`, keep state unchanged, and remain inspectable through idempotency record behavior.
  - [x] Document that operator-disposition labels must come from the architecture state catalog and later UI mapping must be generated from or tested against that catalog.
  - [x] Document the future aggregate-test requirement: every state and every event in the architecture vocabulary needs positive transition or explicit rejection coverage, without adding aggregate tests in this story.
  - [x] Include the architecture source date or review date and the exact state/event vocabulary copied from the C6 source so future changes have an obvious drift point to update before implementation.
- [x] Add lightweight verification. (AC: 1, 2, 3, 4, 6)
  - [x] Prefer a documentation test or script that checks the required files exist and contain no unresolved `TBD` placeholders for the Phase-1-blocking fields.
  - [x] Check that no deliverable includes obvious secret values, provider tokens, production credentials, tenant data, raw file contents, or production-only URLs.
  - [x] Check that verification evidence confirms no OpenAPI spine files, generated SDK outputs, REST/CLI/MCP/server code, authentication middleware, provider adapters, domain aggregates, aggregate tests, CI workflow gates, infrastructure files, or `FolderStateTransitions.cs` were added by this story.
  - [x] Check each required artifact contains `status`, `decision owner`, `approval authority`, `source inputs`, `last reviewed`, `open questions`, `Decision`, `Rationale`, `Verification impact`, and `Deferred implementation` sections.
  - [x] Check C3/C4 artifacts distinguish `approved`, `proposed workshop value`, and `needs human decision`; if any `needs human decision` remains for a Phase-1-blocking value, record the exact blocked consumer and do not claim Phase 1 is unblocked.
  - [x] Check S-2 examples use non-production placeholders and C6 references do not drift from the architecture state/event vocabulary.
  - [x] If an existing test project is available, integrate the check there; otherwise add the lightest local script or documented manual verification consistent with the current scaffold.
  - [x] Run the relevant verification command and, when the scaffold supports it, `dotnet build Hexalith.Folders.slnx`.

### Review Findings

Code review 2026-05-12 — Blind Hunter + Edge Case Hunter + Acceptance Auditor against commit `6098b45^..6098b45`. Acceptance Auditor: all 9 ACs satisfied, no scope-boundary violations. Findings below are verification-strength and content-quality concerns.

Decision-needed (all four resolved 2026-05-12):

- [x] [Review][Decision→Dismiss] C3 approval-state column wording — accepted as-is: the uniform `proposed workshop value; needs human decision` string is acceptable shorthand because every row points to the same Legal+PM blocker and the per-row `Future consumer` column already names the blocked story. [`docs/exit-criteria/c3-retention.md`]
- [x] [Review][Decision→Patch] C3 expanded from 6 task-named categories to 10 — keep all 10 rows; workshop decided the extras (folder metadata, auth claims, diagnostics, commit idempotency) are separate data classes. Promoted to patch P15: add a one-line note in `## Rationale` explaining the expansion, and Patch P8 (assert the 6 mandated categories) still applies as a regression guard. [`docs/exit-criteria/c3-retention.md`]
- [x] [Review][Decision→Patch] C4 "Value" column sentinel magic numbers — restructure into two columns. Promoted to patch P16: split the `Value` column into `Numeric value` (with `—` for non-numeric rows) and `Semantic flag` (free text for sentinel rows; `—` for numeric rows). Eliminates the duplicate `2` ambiguity between `Max query duration` and `Timeout behavior`. [`docs/exit-criteria/c4-input-limits.md`]
- [x] [Review][Decision→Dismiss] On-disk `ExitCriteriaDecisionArtifactTests.cs` divergence from commit `6098b45` — confirmed intentional: extra `AssertNoDownstreamOperationGroups` block was added by commits `f1037a5` (story 1.6 impl) and `50c8495` (story 1.6 review) as a positive ownership guardrail. Already documented in story 1-6 Review Findings. No story 1.4 action. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]

Patches (test-strength + content fixes):

- [x] [Review][Patch] `IsDecisionTableRow` filter is circular and over-broad — `Contains("Limit", OrdinalIgnoreCase)` excludes 6 of 9 C4 rows that legitimately contain the substring (e.g., `input_limit_exceeded`, `response_limit_exceeded`); filter also requires an approval-state keyword to enter the assertion, making the approval-state assertion tautological. Detect table rows by structure (leading `|`, ≥ N cell separators, not the header/separator) and assert approval state on every data row [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:215-220`]
- [x] [Review][Patch] C6 architecture anchor uses ASCII hyphen but `architecture.md` heading uses an em-dash `Workspace State Transition Matrix (C6 — Enumerated)` — anchor will not resolve [`docs/exit-criteria/c6-transition-matrix-mapping.md:12`]
- [x] [Review][Patch] Architecture-vocabulary drift check is uni-directional — test asserts the hardcoded array IS in the artifact but never loads `architecture.md` to verify the artifact mirrors the source. Add a bidirectional check: load `architecture.md` C6 section, extract state/event tokens, assert artifact set == architecture set [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:159-209`]
- [x] [Review][Patch] Placeholder denylist misses common variants — current denylist catches `TBD` and `retain as needed`. Add `T.B.D.`, `TBA`, `XXX`, `<placeholder>`, `FIXME`, `TODO`, `to be determined`, `<unknown>` [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:71-79`]
- [x] [Review][Patch] Secret-material denylist misses common formats — add `BEGIN RSA PRIVATE KEY`, `BEGIN EC PRIVATE KEY`, `BEGIN OPENSSH PRIVATE KEY`, `BEGIN CERTIFICATE`, `aws_access_key_id`, `AKIA[A-Z0-9]{16}` (regex), `xoxb-`, `xoxp-`, `xoxa-`, `xoxr-` (Slack), `ghp_`, `gho_` (GitHub PAT), `sk_(live|test)_` (Stripe), `sk-` (OpenAI). Reuse the same `RawJwt`-style approach as story 1-6's hygiene scan [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:71-79`]
- [x] [Review][Patch] Anchor `status:` / `decision owner:` / `approval authority:` metadata-key checks to beginning-of-line — current `ShouldContain("status:", IgnoreCase)` can be satisfied by prose like "workspace status: terminal", so removing the front-matter line would still pass. Use a regex `^\s*<key>\s*:` per check [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:17-32`]
- [x] [Review][Patch] Replace hard-coded `2026-05-11` review-date evidence with dynamic check — current test bakes in `2026-05-11` per row. When artifacts are legitimately re-reviewed and dates updated, the test will fail. Read the frontmatter `last reviewed:` value and assert each row's "Review date" column equals it [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:118`]
- [x] [Review][Patch] Assert the 6 mandated C3 categories are present by name — story task explicitly enumerated audit metadata / workspace status / provider correlation IDs / read-model views / temporary working files / cleanup records as the minimum set. Add an assertion that the C3 "Data class" column contains all 6 strings; current test would pass even if "Provider correlation IDs" row were deleted [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:50-65`]
- [x] [Review][Patch] Bound `RepositoryRoot()` walk depth and include CWD in the exception — current `RepositoryRoot()` walks ancestors with no max depth; on a broken layout it throws `Repository root was not found.` without the actual `AppContext.BaseDirectory`. Cap at 12 ancestors and include the starting directory in the message [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:249-258`]
- [x] [Review][Patch] Strengthen `ShouldContain("Story", ...)` per-row check — substring matches false-positives like "history", "store". Use a regex `Story \d+(\.\d+)?` to require a story key shape [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]
- [x] [Review][Patch] Add an AC9 "every numeric has units" assertion for C4 — content already includes units in the "Units and boundary semantics" column, but the test doesn't enforce it. Assert each C4 row's units column is non-empty and matches one of `seconds`, `bytes`, `items`, `paths`, or `n/a` [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]
- [x] [Review][Patch] S-2 `requiredSettings` array conflates JwtBearer parameter names (`ValidateIssuer`, etc.) with claim names (`eventstore:tenant`, `eventstore:permission`) — split into two arrays so each assertion category is documented and traceable [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:141-142`]
- [x] [Review][Patch] Run `dotnet build Hexalith.Folders.slnx --no-restore -v:minimal` and record fresh evidence — Debug Log references `dotnet test ... --no-build --no-restore` for the 29-test run; no `dotnet build` evidence corroborates that the new `ExitCriteriaDecisionArtifactTests.cs` actually compiles into the test assembly. Add a fresh build-evidence line to the Debug Log [story file `### Debug Log References`]
- [x] [Review][Patch] `RawJwt` regex matches its own counter-example, making it impossible to document a forbidden JWT shape in the artifact — replace any future counter-example JWT with `eyJ<redacted>` placeholder (no current usage; add as a comment on the regex for future authors) [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:226-228`]
- [x] [Review][Patch][Decision] Add a one-line note in `## Rationale` of `docs/exit-criteria/c3-retention.md` explaining that the table covers 10 data classes — the 6 enumerated in the story task plus 4 the Phase 0.5 workshop classified as separate (folder metadata, auth claims copied into metadata, diagnostics, commit idempotency). Ties to patch P8 (regression guard). [`docs/exit-criteria/c3-retention.md`]
- [x] [Review][Patch][Decision] Restructure C4 `Value` column into two columns: `Numeric value` (numbers + units; `—` for non-numeric rows) and `Semantic flag` (free-text behavior description; `—` for numeric rows). Eliminates duplicate-`2` ambiguity between `Max query duration` and `Timeout behavior` and removes `0`/`1` sentinel magic numbers. Update header, separator row, all 9 data rows, and the `IsDecisionTableRow` filter / column-count assertion in the verification test accordingly. [`docs/exit-criteria/c4-input-limits.md`, `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]

Deferred (pre-existing, downstream-owned, or low-risk):

- [x] [Review][Defer] `ProductionUrl` regex would reject legitimate documentation citations such as `https://learn.microsoft.com/...` if any are added later — no current usage; deferred until a citation outside `.invalid` becomes necessary. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:222-224`]
- [x] [Review][Defer] Opaque/provider-token detection (PASETO, Macaroon, GitHub PATs as non-JWTs, etc.) — `RawJwt` only catches `eyJ`-prefixed JWTs; tracked as part of the broader hygiene-scan vocabulary owned by story 1-6 follow-ups. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:226-228`]
- [x] [Review][Defer] `File.ReadAllText` makes no encoding assertion — BOM / UTF-16 edge cases would silently misread the artifact. Low risk: project standardizes on UTF-8; deferred until an editor introduces non-UTF-8 content. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]
- [x] [Review][Defer] Windows case-insensitive filesystem path normalization could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`) — convention is enforced by code review and PR diff. Deferred. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:9-15`]
- [x] [Review][Defer] C6 maps every state to a single Story 4.1 consumer — if 4.1 splits, all rows need re-pointing. Already captured in the artifact's `open questions` section. Deferred to story 4.1 entry. [`docs/exit-criteria/c6-transition-matrix-mapping.md`]

Dismissed (handled elsewhere, by-design, or no functional impact):

- C6 state-token assertion uses backticks — bold/code-block markup variants would false-positive fail, but the catalog is spec'd in backticks; the strictness is intentional.
- `sprint-status.yaml` edit not validated by the doc-only verification test — sprint-status is governance metadata; deliberately out of doc-verification scope.
- Front-matter is human-readable prose, not parseable YAML — by design; the test reads it line-by-line, not via a YAML parser.
- Test file relies on implicit `System.IO` / `System.Linq` usings — project enables implicit usings; convention.
- "Codex GPT-5" in Agent Model Used — narrative metadata, no functional impact.
- `last reviewed` = creation+1 day with no second reviewer — the story is in `review` status precisely to obtain that second pair of eyes; this code review IS the second pass.
- Keycloak-shaped `.invalid` issuer placeholders (`oidc.local.invalid/realms/hexalith-folders`) — `.invalid` TLD is non-routable per RFC 2606 and satisfies AC9 "clearly non-production".
- Per-row redundant `last reviewed` date column — provides redundancy with frontmatter but does not break verification.
- `.invalid` regex edge with `mailto:` / `ftp://` / other schemes — no current usage; if a non-http URL is added later, the existing regex would not catch it but the contents are still placeholder-safe by review.

## Dev Notes

### Scope Boundaries

- This story authors Phase 0.5 decision artifacts only. It does not implement the Contract Spine, OpenAPI operation inventory, NSwag SDK generation, idempotency hash helpers, parity oracle generation, provider adapters, domain aggregates, REST endpoints, CLI commands, MCP tools, UI components, workers, or CI workflow gates.
- C3 and C4 are Phase-1-blocking because Phase 1 OpenAPI schemas and idempotency TTL semantics consume their values. Do not start `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` in this story.
- C6 is already enumerated in the architecture. This story documents how implementation must map to it; it must not create `FolderStateTransitions.cs` or aggregate tests.
- S-2 validation parameters are already specified in architecture. This story records them in an implementation-ready artifact with environment placeholders; it must not wire authentication middleware.
- Every decision value must show provenance and approval state. If a value is not approved, record it as a proposed workshop value or `needs human decision`; do not present developer-authored estimates as binding stakeholder policy.
- `needs human decision` is an allowed documentation outcome but not an unblocked Phase 1 outcome. Completion notes must name any blocked C3/C4 consumer, S-2 environment pin, or C6 mapping issue left unresolved.
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
- Verification should treat undocumented numeric values and realistic secret-looking placeholders as failures. Examples must be synthetic and visibly non-production.
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
| 2026-05-11 | Advanced elicitation pass applied authority-state, placeholder-safety, boundary-semantics, and drift-detection hardening. | Codex |
| 2026-05-11 | Implemented Phase 0.5 decision artifacts and lightweight verification; marked C3/C4 authority approval as the remaining Phase 1 blocker. | Codex |
| 2026-05-12 | Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) applied 16 patches: C4 column restructured into `Numeric value` + `Semantic flag`, C3 Rationale documents 10-class scope, C6 anchor switched to em-dash, verification test rewritten with row-shape detection, bidirectional architecture drift check, expanded placeholder/secret denylists, anchored metadata regexes, dynamic frontmatter date, mandated-categories regression guard, bounded `RepositoryRoot()` walk. 0 build warnings, 56 tests pass. | Claude |

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

## Advanced Elicitation

- Date/time: 2026-05-11T02:03:59.8559226+02:00
- Selected story key: `1-4-author-phase-0-5-pre-spine-workshop-deliverables`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-4-author-phase-0-5-pre-spine-workshop-deliverables`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Security Audit Personas
  - Failure Mode Analysis
  - Pre-mortem Analysis
  - Self-Consistency Validation
- Reshuffled Batch 2 method names:
  - Architecture Decision Records
  - Comparative Analysis Matrix
  - Challenge from Critical Perspective
  - First Principles Analysis
  - Critique and Refine
- Findings summary:
  - The story needed a stronger distinction between documented unresolved decisions and a genuinely unblocked Phase 1 handoff.
  - C3/C4 values needed explicit approval-state, boundary semantics, consumer mapping, and D-7 commit-TTL inheritance safeguards.
  - S-2 examples needed clearer placeholder safety so documentation cannot accidentally normalize production-looking issuer, audience, token, key, or tenant values.
  - C6 mapping needed a visible drift checkpoint against the architecture state/event vocabulary before later aggregate implementation.
- Changes applied:
  - Added acceptance criteria for non-approved Phase-1-blocking values and verification failures on generic placeholders, secret-looking values, or undocumented numeric limits.
  - Added C3 tasks for per-row approval-state classification and D-7 commit-idempotency TTL inheritance.
  - Added C4 tasks for inclusive boundary semantics, authorization/filter ordering, partial-result versus canonical-error behavior, and future tenant-tunability notes.
  - Added S-2 placeholder safety and C6 architecture vocabulary drift checks.
  - Added verification expectations for blocked consumers, synthetic placeholders, and unresolved human decisions.
- Findings deferred:
  - Legal + PM still own final C3 retention approval where values are not already authoritative.
  - PM still owns final C4 MVP input-limit approval where values are proposed rather than approved.
  - Security/architecture owners still need to pin real per-environment issuer and audience configuration outside this documentation-only story.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-11: `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore --filter FullyQualifiedName~ExitCriteriaDecisionArtifactTests -v:minimal` failed red phase because the four required decision artifacts did not exist.
- 2026-05-11: `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore --filter FullyQualifiedName~ExitCriteriaDecisionArtifactTests -v:minimal` passed after authoring the four artifacts and verification test.
- 2026-05-11: Scope check confirmed no Contract Spine OpenAPI file, `FolderStateTransitions.cs`, CI workflow, appsettings, infrastructure file, or aggregate implementation was added by this story.
- 2026-05-11: `dotnet test .\Hexalith.Folders.slnx --no-build --no-restore -v:minimal` passed: 29 tests across 11 test assemblies.
- 2026-05-11: `dotnet build .\Hexalith.Folders.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after a standalone rerun.
- 2026-05-11: `git diff --check` passed with no whitespace errors.
- 2026-05-12: Code review applied 16 patches (P1-P16). `dotnet build Hexalith.Folders.slnx --no-restore -v:minimal` passed: 0 warnings, 0 errors, 24 projects compiled in ~4.34s.
- 2026-05-12: `dotnet test Hexalith.Folders.slnx --no-build --no-restore -v:minimal` passed: 11 test assemblies, 56 tests, 0 failures, 0 skipped. `Hexalith.Folders.Testing.Tests` rewrite contributes 28 tests (was 4); 7 test methods cover artifact shape, placeholder/secret hygiene, C3/C4 row metadata, C3 mandated-categories regression guard, C4 numeric/semantic-flag column structure, S-2 JwtBearer + claim provenance, and C6 bidirectional architecture drift.

### Completion Notes List

- Confirmed Story 1.1 scaffold, Story 1.2 root/submodule policy, and Story 1.3 fixture/template prerequisites were present. No nested submodules were initialized or updated.
- Authored `c3-retention.md` with concrete proposed retention durations, metadata-only cleanup behavior, tenant-isolation implications, provenance, approval state, review date, downstream consumers, and D-7 commit-idempotency TTL inheritance.
- Authored `c4-input-limits.md` with bounded MVP input limits, units, inclusive boundary semantics, auth/filter ordering, timeout/truncation behavior, audit visibility, OpenAPI mapping notes, provenance, approval state, review date, and downstream consumers.
- Authored `s2-oidc-validation.md` with the frozen JwtBearer validation settings, JWKS refresh intervals, JWT-only behavior, synthetic `.invalid` issuer placeholders, and claim-provenance rules.
- Authored `c6-transition-matrix-mapping.md` with the architecture source, future implementation target path, 11-state catalog, event vocabulary, default `state_transition_invalid` rejection rule, operator-disposition mapping, and future aggregate-test coverage expectations.
- Added focused documentation verification in `ExitCriteriaDecisionArtifactTests` covering required artifact shape, placeholder/secret safety, C3/C4 row metadata, S-2 parameters, C6 vocabulary, and scope leakage.
- C3 and C4 remain proposed workshop values, not approved policy. Story 1.6 Contract Spine authoring is blocked until Legal + PM approve C3 and PM approves C4 or provide replacement values.
- No Contract Spine OpenAPI, generated SDK output, REST/CLI/MCP/server behavior, authentication middleware, provider adapter, domain aggregate, aggregate test, CI workflow gate, infrastructure file, or `FolderStateTransitions.cs` was added.

### File List

- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/exit-criteria/c3-retention.md`
- `docs/exit-criteria/c4-input-limits.md`
- `docs/exit-criteria/s2-oidc-validation.md`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`
- `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`
