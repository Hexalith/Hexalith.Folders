# Story 1.15: Wire safety invariant CI gates

Status: ready-for-dev

Created: 2026-05-13

## Story

As a maintainer,
I want safety invariant gates wired into CI,
so that implementation cannot leak secrets, file contents, or tenant data through generated or runtime artifacts.

## Acceptance Criteria

1. Given `tests/fixtures/audit-leakage-corpus.json` exists, when CI runs, then sentinel-corpus redaction tests execute against configured output channels and fail on any detected file content, token, credential, generated-context, provider payload, tenant data, local absolute path, production URL, or unauthorized-resource leakage.
2. Given the sentinel corpus is currently a seeded synthetic fixture, when this story hardens it, then new samples remain synthetic, metadata-only, explicitly classified, and safe to commit; real tenant names, repository URLs, branch names, provider payloads, file contents, diffs, tokens, credentials, or customer data are never added as examples.
3. Given every signal-emitting component can become a leak path, when safety gates run, then they cover logs, traces, metric labels, events, audit records, projections, provider diagnostics, console payload examples, generated SDK/parity artifacts, OpenAPI examples, Problem Details examples, and developer-facing diagnostics that are present in the repository.
4. Given a channel is not yet implemented, when CI runs, then the gate records a bounded reference-pending or prerequisite-drift diagnostic for that channel instead of silently passing or inventing runtime behavior.
5. Given Story 1.14 owns Contract Spine drift, generated-client, and parity schema CI wiring, when this story adds workflow steps, then it reuses or extends the existing workflow lane without adding duplicate restore/build/test commands or broad release/security jobs outside safety invariant enforcement.
6. Given Story 1.16 owns exit-criteria, idempotency-encoding, tenant-prefixed cache-key lint, pattern-example compilation, and parity completeness gates, when this story is complete, then it does not implement those gates or move their ownership into the safety-invariant workflow.
7. Given sensitive metadata classification is authoritative, when tests scan outputs, then paths, branch names, repository names, commit messages, provider correlation IDs, actor metadata, folder/workspace/task IDs, and audit metadata are handled according to the approved classification/redaction vocabulary rather than simple blanket string deletion.
8. Given unauthorized-resource existence is itself sensitive, when negative cases run, then wrong-tenant, unauthorized, hidden, redacted, missing, unknown, stale, and projection-unavailable examples do not reveal resource existence through status text, counts, ordering, cursor values, stack traces, schema examples, or diagnostics.
9. Given context-query authorization order is tenant access, folder ACL, path policy, then execution, when safety tests inspect search, glob, partial-read, and file-metadata contract examples or available tests, then sentinel values are checked after the authorization boundary and never rely on search-first/filter-later behavior.
10. Given generated artifacts are reviewed in pull requests, when safety gates scan generated clients, parity rows, normalized OpenAPI, schema validation output, and helper diagnostics, then they allow only safe provenance such as operation IDs, schema pointers, content hashes, gate names, repository-relative paths, and synthetic sentinel IDs.
11. Given CI diagnostics are visible outside the local developer machine, when a safety gate fails, then logs include the gate name, repository-relative path, synthetic sample ID, output channel, and bounded classification only; logs must not echo the forbidden value, raw payload, file content, diff, token, credential, local absolute path, production URL, tenant data, or unauthorized resource hint.
12. Given the gates must be locally reproducible, when implementation is complete, then a documented local command runs the same sentinel/redaction checks without requiring Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or nested submodule initialization.
13. Given active Story 1.10 through Story 1.14 work may still be dirty or reference-pending, when implementation starts, then the developer inspects the current OpenAPI, generated-artifact expectations, parity schema, contract docs, and existing test projects before assuming which output channels are implemented.
14. Given this story wires safety invariant gates only, when implementation is complete, then it does not add runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects outside deterministic tests/tools, SDK generation policy, parity-oracle derivation, CLI commands, MCP tools, UI pages, Dapr policy conformance, provider drift jobs, release publishing, tenant cache-key lint, exit-criteria checks, or nested-submodule initialization.
15. Given safety output channels can drift as Stories 1.10 through 1.14 land, when this story implements scanning, then a channel inventory or manifest defines each scanned channel, owning story, artifact/test source, prerequisite status, and whether absence is `reference-pending` or `prerequisite-drift`.
16. Given the sentinel corpus is the authoritative safety vocabulary for this gate, when tests load it, then unknown classification labels, local synonyms, missing forbidden-surface lists, or missing allowed-provenance lists fail before any artifact scan runs.
17. Given telemetry can leak outside message bodies, when safety gates scan traces and metrics, then they inspect tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage in addition to log text.
18. Given generated artifact scans overlap adjacent CI stories, when this story scans OpenAPI, generated SDK output, parity artifacts, Problem Details examples, developer diagnostics, or CI logs, then it checks leakage and safe provenance only; it does not validate Contract Spine drift, parity completeness, schema derivation, client-generation correctness, or exit criteria.

## Tasks / Subtasks

- [ ] Confirm safety-gate prerequisites and current artifact ownership. (AC: 1, 2, 3, 4, 5, 6, 12, 13)
  - [ ] Inspect `.github/workflows/`; if Story 1.14 has not created a workflow yet, create only the focused safety workflow or a narrowly named job that can later be composed with Story 1.14.
  - [ ] Inspect `tests/fixtures/audit-leakage-corpus.json`, `tests/README.md`, `_bmad-output/project-context.md`, and `_bmad-output/planning-artifacts/architecture.md` sections on sentinel redaction, sensitive metadata classification, authorization order, and enforcement guidelines.
  - [ ] Inspect Story 1.10 through Story 1.14 artifacts before assuming implemented OpenAPI examples, generated-client paths, parity rows, or CI workflow names.
  - [ ] Inspect existing test projects for the best home for safety checks, especially `tests/Hexalith.Folders.Contracts.Tests/`, `tests/Hexalith.Folders.Testing.Tests/`, `tests/Hexalith.Folders.Server.Tests/`, `tests/Hexalith.Folders.UI.Tests/`, and any generated-artifact tests created by Stories 1.12 through 1.14.
  - [ ] Create or update a channel inventory or manifest that names each scanned channel, owning story, artifact or test source, prerequisite status, and safe absence diagnostic.
  - [ ] Treat missing runtime channels, missing generated artifacts, absent workflow files, or placeholder-only safety helpers as prerequisite drift unless the gate can fail closed with a targeted diagnostic.
  - [ ] Do not initialize or update nested submodules. If submodules are needed for local validation, initialize only the root-level modules listed in `AGENTS.md`.
- [ ] Harden the sentinel corpus contract. (AC: 1, 2, 7, 8, 11)
  - [ ] Preserve `tests/fixtures/audit-leakage-corpus.json` as the single normative cross-project sentinel corpus.
  - [ ] Add only synthetic sentinel samples needed to prove file-content, token, credential, generated-context, provider-payload, tenant-data, unauthorized-resource, local-path, production-URL, path, branch, repository, commit-message, actor, correlation, diff, diagnostic-echo, and safe-provenance rules.
  - [ ] Require each sentinel to declare classification, category, forbidden output surfaces, allowed provenance-safe representations, and whether it participates in positive or intentionally contaminated negative-control fixtures.
  - [ ] Add schema or fixture-contract tests that fail if a sample lacks `synthetic_sentinel`, `synthetic_data_only`, classification, category, ID, or safe notes.
  - [ ] Add tests that fail when unknown classification labels or ad hoc local vocabulary appear outside the sentinel corpus contract.
  - [ ] Add tests that fail if corpus samples look like real tenant IDs, real provider URLs, real repository names, real local absolute paths, real production hosts, real secrets, or raw content/diff excerpts.
  - [ ] Keep new categories reviewer-visible in the corpus; do not hide policy expansion inside test code only.
- [ ] Add or wire safety gate test entry points. (AC: 1, 3, 4, 7, 8, 9, 10, 11, 12)
  - [ ] Prefer focused test projects or repository tools that can run locally and in CI with the same command.
  - [ ] Scan generated and checked-in artifacts through structured parsers where practical: JSON for fixtures/schema files, YAML for OpenAPI/parity artifacts, and targeted text checks for docs or generated diagnostics.
  - [ ] Cover logs/traces/metrics/events/audit/projections/provider diagnostics through available examples, fixtures, or channel-specific test seams. For channels not implemented yet, emit prerequisite-drift evidence rather than success.
  - [ ] Include tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage in telemetry scans instead of scanning only message strings.
  - [ ] Include at least one intentionally contaminated fixture per scan family so the gate proves forbidden values are detected without printing the forbidden values.
  - [ ] Validate OpenAPI examples and Problem Details examples for metadata-only fields, safe-denial shape, redaction shape, bounded classification, and absence of raw payload values.
  - [ ] Validate generated SDK/parity/gate diagnostics for leakage and safe provenance only, without using broad `git diff --exit-code` across unrelated active development files and without asserting parity completeness, derivation, or generated-client correctness.
  - [ ] Check context-query examples and tests for authorization-before-observation ordering and no search-first/filter-later leakage.
  - [ ] Keep failure messages safe: report gate name, output channel, repository-relative artifact path, rule ID, synthetic sample ID, classification, and remediation hint only; never report the leaked value itself.
  - [ ] Emit bounded missing-channel diagnostics such as `SAFETY-CHANNEL-MISSING` or `SAFETY-PREREQUISITE-DRIFT` with channel name, owner, and remediation hint only.
- [ ] Wire the CI job for safety invariants. (AC: 1, 5, 6, 11, 12, 14)
  - [ ] Add or update a focused GitHub Actions job for safety invariant gates, preferably in the workflow established by Story 1.14 if it exists by implementation time.
  - [ ] Use one repository-root offline command, preferably backed by `tests/tools`, that CI invokes unchanged and that respects `global.json`, central package management, and the root-level submodule policy.
  - [ ] Run restore/build/test steps only as needed for this safety gate lane; do not duplicate the full release pipeline.
  - [ ] Keep all CI diagnostics repository-relative and metadata-only.
  - [ ] Do not add Dapr policy conformance, provider live drift, package publishing, release evidence, exit-criteria, cache-key tenant-prefix, idempotency-encoding, C6 matrix, or parity completeness jobs in this story.
- [ ] Document developer and reviewer usage. (AC: 2, 4, 7, 8, 11, 12, 13)
  - [ ] Add or update focused documentation such as `docs/contract/safety-invariant-ci-gates.md`.
  - [ ] Document local commands, CI job names, scanned inputs, output-channel coverage, channel inventory fields, prerequisite-drift categories, and safe diagnostic format.
  - [ ] Document how to add new synthetic sentinel categories and what reviewer approval is required.
  - [ ] Document how redacted, unknown, missing, hidden, unauthorized, stale, and unavailable states differ without leaking resource existence.
  - [ ] Add a reviewer checklist covering synthetic-only corpus changes, forbidden-value echo prevention, safe CI diagnostics, channel inventory coverage, generated-artifact leakage-only scope, and Story 1.16 scope boundaries.
  - [ ] Document that Story 1.16 still owns tenant-prefixed cache-key lint and exit-criteria gates.
- [ ] Run verification. (AC: 1, 2, 3, 11, 12, 14)
  - [ ] Run the focused safety invariant tests.
  - [ ] Run the workflow-equivalent local command from the repository root.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if active contract work and prerequisite drift allow it; if blocked, record the exact blocker.
  - [ ] Confirm no nested submodules were initialized.
  - [ ] Confirm no unrelated active Story 1.10 through Story 1.14 files were modified.

## Dev Notes

### Scope Boundaries

- This story wires CI and local gate entry points for safety invariant enforcement: sentinel redaction, forbidden leakage scanning, metadata-only diagnostics, and safe example validation.
- Allowed implementation areas are:

```text
.github/workflows/
tests/fixtures/audit-leakage-corpus.json
tests/Hexalith.Folders.Contracts.Tests/
tests/Hexalith.Folders.Testing.Tests/
tests/Hexalith.Folders.Server.Tests/
tests/Hexalith.Folders.UI.Tests/
tests/tools/
docs/contract/safety-invariant-ci-gates.md
```

- Equivalent file names are acceptable when they preserve the same ownership boundaries.
- Do not implement runtime redaction policy, domain behavior, provider behavior, CLI/MCP/UI feature work, Contract Spine operation groups, NSwag generation, parity-oracle derivation, Dapr policy conformance, cache-key tenant-prefix lint, exit-criteria checks, release publishing, or live provider drift checks.
- This story may add test/tooling safety checks over generated or checked-in artifacts, but it must not hand-edit generated outputs owned by Stories 1.12 or 1.13.

### Current Repository State To Inspect

- `.github/workflows/` is currently absent unless Story 1.14 creates it before implementation begins.
- `tests/fixtures/audit-leakage-corpus.json` exists as a minimal synthetic sentinel corpus with secret-shaped, credential-shaped, path, branch, commit-message, and provider-diagnostic placeholder samples.
- `tests/README.md` names redaction sentinel corpus gates as future CI work alongside parity schema, C6 matrix, cache-key tenant prefix, and provider drift checks.
- `_bmad-output/project-context.md` states that sentinel tests must iterate `tests/fixtures/audit-leakage-corpus.json` across logs, traces, metrics labels, events, audit records, projections, provider diagnostics, console views, and error responses.
- Architecture concern #6 makes the sentinel corpus normative; concern #17 and decision S-6 define sensitive metadata classification; concern #18 defines context-query authorization order; C10 cache-key lint is a separate Story 1.16 concern.
- Existing contract tests under `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` already parse OpenAPI with structured helpers and are likely useful for example and schema scanning.
- Active Story 1.10 review work may leave dirty OpenAPI, contract docs, and contract-test files. This story must inspect current state but must not absorb Story 1.10 implementation scope.

### Gate Requirements

- Treat the sentinel corpus as an input fixture, not as the policy engine. Tests should verify both corpus integrity and output-channel behavior.
- Treat `tests/fixtures/audit-leakage-corpus.json` as the authoritative vocabulary for this gate: classification labels, sentinel IDs, forbidden surfaces, allowed safe provenance, and synthetic-only metadata must be declared there before tests use them.
- Maintain a safety channel inventory or manifest for this story. Each entry should name the output channel, owning story or artifact family, scanned artifact/test source, prerequisite status, and the bounded diagnostic to emit when absent.
- Use structured parsing where possible. Prefer JSON parsing for the corpus and schema fixtures, YAML parsing for OpenAPI/parity artifacts, and targeted text scanning only where a structured parser is not available.
- Safety gates must fail closed when a channel claims coverage but provides no artifact, no test seam, or only placeholder behavior.
- Diagnostics may include gate name, repository-relative path, synthetic sample ID, category, classification, rule ID, owning story, operation ID, schema pointer, content hash, output-channel name, and remediation hint.
- Diagnostics must never echo forbidden values, real secrets, file contents, diffs, raw provider payloads, generated context payloads, local absolute paths, production URLs, real tenant data, or unauthorized-resource hints.
- Missing-channel diagnostics must use bounded categories such as `SAFETY-CHANNEL-MISSING` or `SAFETY-PREREQUISITE-DRIFT` and must not include discovered runtime data, sample payloads, serialized generated snippets, tenant IDs, resource IDs, provider response bodies, timestamps, cache keys, counts, cursors, or path fragments.
- Keep tests offline and deterministic. They must not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or initialized nested submodules.
- Generated artifact scans are leakage-only checks. They may allow safe provenance such as operation IDs, schema pointers, tool names, rule IDs, content hashes, fixture names, sentinel categories, and redaction markers, but must not assert drift, parity completeness, schema derivation, generated-client correctness, or release readiness.

### Previous Story Intelligence

- Story 1.3 seeded `tests/fixtures/audit-leakage-corpus.json` as a placeholder fixture for future redaction and leakage checks.
- Story 1.5 made metadata-only idempotency and adapter parity rules authoritative; helper and parity diagnostics must remain safe by construction.
- Story 1.6 created the Contract Spine foundation and extension vocabulary; this story scans or validates examples but does not define new OpenAPI operation metadata.
- Stories 1.7 through 1.11 author operation groups and audit/ops-console read contracts. This story checks their examples and diagnostics for leakage, not their business semantics.
- Story 1.12 owns generated SDK clients and idempotency helper generation. This story may scan their generated output and diagnostics after they exist, but must not edit generated SDK policy.
- Story 1.13 owns C13 parity oracle generation. This story may scan parity outputs and diagnostics after they exist, but must not derive rows or add parity completeness rules.
- Story 1.14 owns Contract Spine drift, generated-client golden-file, parity schema, and workflow wiring. This story should reuse the CI lane where possible and remain focused on safety invariants.
- Story 1.16 owns exit-criteria presence, idempotency-encoding equivalence, pattern-example compilation, tenant-prefixed cache-key lint, and parity completeness gates.

### Latest Technical Notes

- GitHub Actions and .NET setup details should follow the workflow guidance captured in Story 1.14: use checked-in `global.json`, central package management, repository-root commands, and locally reproducible `dotnet` invocations.
- `actions/setup-dotnet` NuGet caching remains optional. Do not add cache behavior in this story unless lock-file support and safe cache keys already exist; tenant-prefixed cache-key lint is owned by Story 1.16.
- Use the repository's existing xUnit, Shouldly, and YamlDotNet testing patterns rather than adding a new scanner framework unless an existing parser cannot express the check.

### Testing Guidance

- Good positive cases: synthetic sentinel samples are classified and safe; safe diagnostics report only sample IDs/categories; redacted values are distinguishable from unknown/missing for authorized examples; generated artifacts contain safe provenance only.
- Good negative cases: a fake token-shaped sentinel appears in a Problem Details example, a generated diagnostic echoes a forbidden value, an OpenAPI example contains a local absolute path, a console payload fixture silently drops redaction state, an unauthorized example reveals resource existence through count/order/cursor metadata, or a channel claims coverage but has no test seam.
- Keep assertions precise and bounded. Avoid broad secret-scanning claims that cannot be proven by the repository fixtures and tests.
- If a future runtime channel is absent, assert the prerequisite-drift marker instead of creating a placeholder runtime implementation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.15: Wire safety invariant CI gates`
- `_bmad-output/planning-artifacts/architecture.md#Sentinel redaction`
- `_bmad-output/planning-artifacts/architecture.md#Sensitive metadata classification`
- `_bmad-output/planning-artifacts/architecture.md#Authorization order`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/1-14-wire-contract-spine-drift-and-generated-client-ci-gates.md`
- `_bmad-output/implementation-artifacts/1-13-generate-the-c13-parity-oracle.md`
- `_bmad-output/implementation-artifacts/1-12-wire-nswag-sdk-generation-with-idempotency-helpers.md`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/README.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- Workflow files belong under `.github/workflows/`.
- Sentinel corpus and shared leakage fixtures belong under `tests/fixtures/`.
- Focused safety tests should live in the smallest existing test project that owns the scanned artifact.
- Test helpers or tools may live under `tests/tools/` when they are reusable from local commands and CI.
- Human-readable gate documentation may live at `docs/contract/safety-invariant-ci-gates.md`.
- Do not place safety gate tooling in runtime projects unless the runtime project already owns the emitted channel being tested.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-13 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-15 | Party-mode review applied channel inventory, bounded diagnostic, vocabulary authority, negative-control, telemetry, generated-artifact scope, and reviewer checklist hardening. | Codex |

## Party-Mode Review

- Date/time: 2026-05-15T12:05:44Z
- Selected story: 1-15-wire-safety-invariant-ci-gates
- Command/skill invocation used: `/bmad-party-mode 1-15-wire-safety-invariant-ci-gates; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Channel ownership needed a concrete inventory or manifest so implemented, reference-pending, and prerequisite-drift channels are explicit.
  - Bounded prerequisite-drift diagnostics needed allowed and forbidden output fields to prevent CI logs from echoing sensitive data.
  - The sentinel corpus needed to be named as the authoritative classification/redaction vocabulary source for this gate.
  - Generated artifact scanning needed explicit leakage-only boundaries to avoid absorbing Story 1.14 or Story 1.16 responsibilities.
  - Telemetry checks needed to include tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage, not only message strings.
  - Negative controls and reviewer-facing guidance were needed to prove the scanner fails safely and remains synthetic-only.
- Changes applied:
  - Added ACs for channel inventory, authoritative corpus vocabulary, telemetry scan targets, and generated-artifact leakage-only scope.
  - Added tasks for channel inventory, sentinel forbidden-surface and allowed-provenance lists, unknown classification failure, intentionally contaminated negative-control fixtures, safe failure-message fields, bounded missing-channel diagnostics, single offline local/CI command reuse, and reviewer checklist documentation.
  - Expanded Gate Requirements with authoritative vocabulary, channel manifest, allowed diagnostics, forbidden missing-channel fields, and generated-artifact leakage-only guidance.
- Findings deferred:
  - Runtime redaction handlers, provider behavior changes, SDK/parity derivation, Contract Spine drift checks, parity completeness, idempotency encoding, tenant cache-key lint, exit criteria, pattern compilation, release jobs, and broad workflow orchestration remain outside Story 1.15.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
