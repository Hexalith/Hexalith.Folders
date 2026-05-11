# Story 1.5: Finalize idempotency equivalence and adapter parity rules

Status: review

Created: 2026-05-10

## Story

As an adapter implementer,
I want idempotency equivalence and parity dimensions defined before endpoints are authored,
so that REST, SDK, CLI, and MCP cannot drift on operation identity or error handling.

## Acceptance Criteria

1. Given the approved command and query inventory, when this story is complete, then a contract-rules artifact exists under `docs/contract/` that enumerates every MVP command/query planned for the Contract Spine and classifies each operation as mutating command, query/status operation, audit operation, or operations-console projection query.
2. Given every mutating command in that inventory, when idempotency metadata is defined, then each mutating command has a lexicographically ordered `x-hexalith-idempotency-equivalence` field list, an idempotency-key sourcing rule, an idempotency TTL tier, duplicate-equivalent outcome, conflicting-payload outcome, and required correlation/task identity behavior.
3. Given every non-mutating operation in that inventory, when parity metadata is defined, then each query/status, audit, and console operation has a read-consistency class, authorization outcome class, safe denial shape, audit metadata keys, correlation behavior, terminal or projection-state expectations, and explicit non-idempotent semantics.
4. Given the architecture Adapter Parity Contract, when adapter rules are finalized, then SDK, CLI, and MCP sourcing and projection rules are recorded for idempotency keys, correlation IDs, task IDs, credentials, pre-SDK errors, post-SDK error projection, CLI exit codes, and MCP failure kinds.
5. Given the C13 parity oracle requirements, when this story is complete, then `tests/fixtures/parity-contract.schema.json` is updated or confirmed to validate both transport-parity columns and behavioral-parity columns, including `auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`, `pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, and `mcp_failure_kind`.
6. Given the idempotency encoding-equivalence risk, when this story is complete, then `tests/fixtures/idempotency-encoding-corpus.json` is updated or confirmed to cover NFC, NFD, NFKC, NFKD, zero-width-joiner, string casing, and ULID casing cases with synthetic metadata-only values.
7. Build or documentation verification for this story succeeds without authoring `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, implementing NSwag generation, generating `tests/fixtures/parity-contract.yaml`, creating REST/CLI/MCP/SDK code, running Dapr sidecars, running Aspire, contacting GitHub or Forgejo, using provider credentials, using tenant data, using production secrets, or initializing nested submodules.
8. Given the idempotency and parity rules are authored, when they are reviewed for adopter-facing behavior, then the artifact explicitly separates finalized MVP rules from deferred Contract Spine, SDK, CLI, MCP, and REST implementation work; tenant-scoped equivalence is mandatory; negative examples cover cross-tenant key reuse, changed payloads, changed credential scope, missing/malformed keys, pre-SDK validation failures, post-SDK/service failures, and non-equivalent replay.
9. Given later stories will consume this artifact mechanically, when this story is complete, then the rules document uses stable table headings and column names for operation inventory, mutating-command equivalence, non-mutating read consistency, adapter outcome parity, deferred owner, and verification coverage so Story 1.6+ and Story 1.13 can translate the rules without re-interpreting prose.

## Tasks / Subtasks

- [x] Inspect prerequisites and preserve story boundaries. (AC: 1, 7)
  - [x] Confirm Story 1.3 fixture seeds are present before changing `tests/fixtures/parity-contract.schema.json` or `tests/fixtures/idempotency-encoding-corpus.json`.
  - [x] Confirm Story 1.4 Phase 0.5 deliverables exist, especially `docs/exit-criteria/c3-retention.md` and `docs/exit-criteria/c4-input-limits.md`, or record the exact missing prerequisite without fabricating values.
  - [x] Do not initialize or update nested submodules; root-level submodules are read-only references unless the user explicitly asks for nested submodules.
  - [x] Do not author the OpenAPI Contract Spine file in this story. Story 1.6 owns the foundation and shared extension vocabulary, and later Epic 1 stories own operation groups.
  - [x] Do not generate SDK code, REST handlers, CLI commands, MCP tools, parity result rows, NSwag configuration, or behavior in `Hexalith.Folders.Contracts`.
- [x] Author the operation metadata rules artifact. (AC: 1, 2, 3)
  - [x] Create `docs/contract/idempotency-and-parity-rules.md`, unless an existing contract-rules document clearly owns the same content.
  - [x] Add a decision-record section that separates finalized MVP rules from deferred OpenAPI, SDK, REST, CLI, MCP, generated-helper, persistence, and parity-oracle implementation work.
  - [x] Include the approved MVP command/query inventory from PRD command/query contract: `ValidateProviderReadiness`, `CreateFolder`, `BindRepository`, `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `AddFile`, `ChangeFile`, `RemoveFile`, `CommitWorkspace`, `GetWorkspaceStatus`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, and `GetAuditTrail`.
  - [x] Add any additional operation names that Story 1.4 or the current architecture has made unavoidable for Phase 1, such as effective permissions, folder lifecycle/archive, provider support evidence, cleanup status, and operations-console projection queries. Mark them as "Phase 1 inventory candidate" if their exact OpenAPI operation ID remains for Story 1.6+ to freeze.
  - [x] For each operation, classify command/query type, mutating versus non-mutating behavior, idempotency requirement, read-consistency expectation where applicable, resource scope, authoritative tenant source, required task/correlation identity, lock requirement, expected state/projection family, canonical error categories, audit metadata keys, parity surfaces, and deferred implementation owner.
  - [x] Use stable, machine-checkable table headings and column names for operation inventory, idempotency-equivalence fields, read-consistency class, parity dimensions, adapter-specific outcomes, deferred owner, and verification coverage. Avoid relying on narrative-only rules for data Story 1.6+ or Story 1.13 must consume.
  - [x] Keep operation IDs stable enough for Story 1.6+ to translate into OpenAPI, but do not create endpoint paths or schemas here unless needed to explain parity metadata.
- [x] Define idempotency equivalence for every mutating command. (AC: 2)
  - [x] Define the adopter-facing rule as: same tenant, same operation intent, same semantic request, same target resource identity, same credential scope where behavior changes, and same idempotency key are required for equivalent replay.
  - [x] For repository/folder creation or binding commands, include identity-defining fields only; exclude timestamps, generated operation IDs, status labels, diagnostic text, and projection-only fields from equivalence.
  - [x] For workspace preparation, include tenant/folder/repository binding identity, task ID, branch/ref policy, provider binding reference, and workspace policy inputs needed to prevent duplicate workspaces.
  - [x] For lock acquisition and release, include tenant/folder/workspace scope, task ID, lock intent, expected lock token or owner where applicable, and lease policy fields that affect behavior.
  - [x] For file add/change/remove, include operation ID, path metadata, content hash or metadata reference, file-operation kind, task ID, workspace scope, and path-policy-relevant metadata; never include raw file content or diffs in rules or examples.
  - [x] For commit, include task ID, workspace scope, branch/ref target, changed-path metadata digest, commit-message classification metadata, author metadata reference, and operation ID. Tie commit TTL to C3 retention rather than the 24-hour mutation tier.
  - [x] State that same key across tenants, changed payload semantics, changed credential scope, changed command intent, or changed target resource identity is non-equivalent and must not reveal prior request metadata.
  - [x] State that fields are ordered lexicographically for `x-hexalith-idempotency-equivalence` and that consumers must use generated `ComputeIdempotencyHash()` helpers after Story 1.12 instead of hand-rolling canonicalization.
  - [x] Define how the rules artifact classifies duplicate JSON keys, null versus omitted values, percent-encoded path equivalence, Unicode normalization form, and casing-sensitive identifiers as equivalent, non-equivalent, or parser-rejected input. Keep parser-policy choices limited to documenting future canonicalization expectations; do not implement canonicalization in this story.
- [x] Define read-consistency and parity dimensions for non-mutating operations. (AC: 3)
  - [x] Assign read consistency classes using the architecture vocabulary: `snapshot-per-task`, `read-your-writes`, or `eventually-consistent`.
  - [x] For context queries, include C4-derived bounds, path policy, include/exclude policy, binary/large-file policy, range limits, result limits, timeout behavior, truncation behavior, and included/excluded audit visibility.
  - [x] For audit and timeline queries, require metadata-only responses and C3 retention awareness.
  - [x] For status and operations-console projections, distinguish accepted command state from projected state when projection lag exists, and include freshness/correlation metadata.
  - [x] Record that non-mutating operations do not accept `Idempotency-Key`; they still require correlation behavior and safe authorization-denial parity.
  - [x] Require safe-denial examples for non-mutating reads to avoid revealing whether a tenant, folder, path, task, provider binding, or audit record exists when credentials are missing, wrong-tenant, or insufficient-scope.
- [x] Finalize adapter parity contract details. (AC: 4)
  - [x] Copy the architecture Adapter Parity Contract into an implementation-facing section or reference it directly from `docs/contract/idempotency-and-parity-rules.md` with all required dimensions intact.
  - [x] Preserve SDK behavior: caller-provided idempotency key or DI provider, no SDK auto-generation; correlation may be caller-provided or generated by SDK provider; task ID is caller-provided for task-scoped operations.
  - [x] Preserve CLI behavior: `--idempotency-key` or `--allow-auto-key` for mutating commands, `--correlation-id`, required `--task-id` for task-scoped commands, credential precedence `HEXALITH_TOKEN` then `~/.hexalith/credentials.json` then `--token`, and canonical exit codes 0, 64-75, and 1.
  - [x] Preserve MCP behavior: required `idempotencyKey` on mutating tool inputs, optional `correlationId`, required `taskId` for task-scoped tools, `auth.token` or `auth.tokenFile` configuration, and the canonical failure-kind set.
  - [x] Add a compact SDK/CLI/MCP outcome table covering success, idempotent replay, conflict/non-equivalent replay, missing credentials, wrong-tenant or insufficient-scope credentials, invalid input before SDK call, service-side rejection, accepted task, failed task, unknown task, and post-SDK error projection.
  - [x] Assert that CLI/MCP wrapping the SDK does not erase behavioral parity dimensions; pre-SDK errors, post-SDK projection, idempotency-key sourcing, correlation sourcing, and credential sourcing remain testable.
- [x] Harden parity schema and encoding fixtures only as far as this story requires. (AC: 5, 6)
  - [x] Update `tests/fixtures/parity-contract.schema.json` only for the row-shape and required-column validation needed by C13. Do not generate `tests/fixtures/parity-contract.yaml`.
  - [x] Ensure the parity schema can distinguish pre-SDK validation failures, SDK/client errors, server contract errors, credential/authentication failures, tenant authorization failures, task lifecycle failures, CLI exit-code mapping, and MCP failure-kind mapping.
  - [x] Ensure the parity schema rejects or flags unknown adapter names, unknown failure kinds, missing correlation/idempotency/task ID expectations, and ambiguous credential-sourcing rules.
  - [x] Update `tests/fixtures/idempotency-encoding-corpus.json` only with synthetic metadata-only cases needed by future `ComputeIdempotencyHash()` tests. Do not implement hash helpers.
  - [x] Include equivalent and non-equivalent encoding examples for canonical and non-canonical string forms, casing, ordering, whitespace, null versus omitted values, percent-encoding, Unicode normalization, zero-width joiner, ULID casing, malformed idempotency keys, and duplicate-key handling where the chosen parser policy can be documented safely.
  - [x] Preserve any existing ownership notes from Story 1.3 and add notes that Story 1.12 and Story 1.13 own helper generation and oracle generation.
  - [x] Ensure fixture changes contain no real tenant IDs, provider tokens, remote URLs with credentials, file contents, diffs, generated context payloads, production issuer URLs, or secrets.
- [x] Add focused verification. (AC: 5, 6, 7)
  - [x] Prefer a lightweight script, documentation test, or existing test project check that parses `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, and `tests/fixtures/idempotency-encoding-corpus.json`.
  - [x] Verify that every mutating command has an idempotency-equivalence field list and every query has a read-consistency class.
  - [x] Verify that required behavioral-parity columns are present in the parity schema.
  - [x] Verify that the encoding corpus is parseable and contains the Unicode/casing categories required by this story.
  - [x] Verify that the rules document contains the stable table headings and column names required by AC9, including at least one row for each MVP operation and each Phase 1 inventory candidate explicitly marked as candidate or deferred.
  - [x] Verify negative-scope guardrails: no OpenAPI spine file, NSwag config, generated SDK output, REST/CLI/MCP implementation, parity result rows, domain behavior in Contracts, CI workflow gate, or nested-submodule initialization was added by this story.
  - [x] Run `dotnet build Hexalith.Folders.slnx` when the scaffold supports it. If build is blocked by prior scaffold work, record the exact prerequisite instead of expanding this story's scope.

## Dev Notes

### Scope Boundaries

- This story produces implementation-facing contract rules and fixture/schema hardening only.
- The deliverable is a normative rules document plus fixture/schema hardening. It must not become Contract Spine authoring, adapter implementation, parity result generation, generated client work, or runtime persistence design.
- Do not author `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. Story 1.6 owns the OpenAPI foundation and extension vocabulary; Stories 1.7 through 1.11 own operation groups.
- Do not wire NSwag SDK generation, generate `ComputeIdempotencyHash()`, generate `tests/fixtures/parity-contract.yaml`, implement REST endpoints, implement CLI commands, implement MCP tools, implement SDK methods, implement domain aggregates, or add CI workflow gates.
- Do not change the architecture policy that `Hexalith.Folders.Contracts` remains behavior-free.
- Do not modify sibling submodules (`Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`). Use them only as read-only references.
- Do not treat narrative prose as the only source of truth for required operation metadata. The rules artifact must expose enough stable table structure for later OpenAPI and parity-oracle stories to consume without guessing hidden intent.

### Expected Deliverables

Expected new or updated files:

```text
docs/contract/idempotency-and-parity-rules.md
tests/fixtures/parity-contract.schema.json
tests/fixtures/idempotency-encoding-corpus.json
```

Optional verification artifact, if no existing test project can host the check:

```text
tests/tools/parity-oracle-generator/README.md
```

or another lightweight local verification file that fits the scaffold. Do not create a parallel test framework if the existing scaffold already has an appropriate project or script location.

### Decision Record Requirements

`docs/contract/idempotency-and-parity-rules.md` must include a short decision-record section with:

- Finalized MVP rules: tenant-scoped idempotency equivalence, required idempotency keys for replay-sensitive mutating commands, SDK as the typed canonical client, CLI/MCP as behavioral wrappers, metadata-only examples, and behavior-free Contracts.
- Deferred work: OpenAPI path/schema/header shape, NSwag generation, generated SDK method signatures, CLI command syntax, MCP tool names/envelopes, idempotency persistence implementation, full parity rows, C13 oracle generation, and final C3/C4 Contract Spine mapping.
- Human-decision state for any rule not already settled by PRD, architecture, Story 1.3, or Story 1.4. Do not encode contested product behavior as fixture truth.
- Machine-checkable table structure for operation inventory, mutating-command equivalence, non-mutating read consistency, adapter parity outcomes, deferred owner, and verification coverage. If a required column cannot be populated from approved sources, mark the row as a deferred decision instead of inventing a value.

### Operation Inventory Seed

Start with the PRD command/query DTO inventory:

| Operation | Initial classification | Required rule family |
| --- | --- | --- |
| `ValidateProviderReadiness` | query/status or safe check | correlation, safe denial, readiness evidence, provider diagnostic metadata |
| `CreateFolder` | mutating command | idempotency equivalence, authorization, audit metadata, folder lifecycle state |
| `BindRepository` | mutating command | idempotency equivalence, provider binding identity, branch/ref policy, audit metadata |
| `PrepareWorkspace` | mutating command | idempotency equivalence, task ID, workspace scope, provider unknown-outcome behavior |
| `LockWorkspace` | mutating command | idempotency equivalence, task ID, lock scope, C6 state behavior |
| `ReleaseWorkspaceLock` | mutating command | idempotency equivalence, task ID, lock token/owner behavior |
| `AddFile` | mutating command | idempotency equivalence, path metadata, content hash/reference, lock requirement |
| `ChangeFile` | mutating command | idempotency equivalence, path metadata, content hash/reference, lock requirement |
| `RemoveFile` | mutating command | idempotency equivalence, path metadata, lock requirement, metadata-only audit |
| `CommitWorkspace` | mutating command | idempotency equivalence, commit TTL from C3, unknown-outcome/reconciliation behavior |
| `GetWorkspaceStatus` | query/status | read consistency, projection freshness, state/terminal expectation |
| `ListFolderFiles` | context query | read consistency, C4 bounds, path policy, metadata-only denial audit |
| `SearchFolderFiles` | context query | read consistency, C4 bounds, path policy, result limits |
| `ReadFileRange` | context query | read consistency, C4 range/byte bounds, redaction/binary policy |
| `GetAuditTrail` | audit query | metadata-only, C3 retention, pagination/filtering, authorization denial parity |

If implementation adds `GetEffectivePermissions`, `ArchiveFolder`, `GetFolderLifecycleStatus`, `GetProviderSupportEvidence`, `GetOperationTimeline`, or operations-console projection queries to satisfy already-approved requirements, classify them in the same artifact and label whether Story 1.6+ must freeze exact operation IDs.

### Idempotency Rules

- Every mutating command carries an `Idempotency-Key`; non-mutating queries do not.
- Equivalence is tenant-scoped. The same idempotency key in another tenant, another command intent, another target resource identity, another semantic payload, or another behavior-affecting credential scope is non-equivalent and must not expose prior metadata.
- Equivalence lists are declarative and ordered lexicographically inside `x-hexalith-idempotency-equivalence`.
- Generated helpers after Story 1.12 own canonicalization. No adapter, caller, CLI command, MCP tool, or test should hand-roll the hash algorithm.
- Same key plus equivalent payload returns the same logical result.
- Same key plus different payload returns canonical `idempotency_conflict`.
- Mutation-tier TTL is 24 hours. Commit-tier TTL inherits C3 retention because commit reconciliation and audit reconstruction need longer evidence.
- Idempotency records must never store file contents, diffs, provider tokens, credential material, secrets, or unauthorized resource existence.

### Adapter Parity Rules

- SDK, REST, CLI, and MCP preserve the same canonical category, code, retryable flag, client action, correlation ID behavior, audit metadata, and terminal/projection state for equivalent inputs.
- SDK and REST own transport parity; CLI and MCP own behavioral parity dimensions that SDK wrapping does not normalize.
- SDK/CLI/MCP rules may describe sourcing, validation timing, and projection behavior, but they must not define new domain behavior outside the canonical contract.
- CLI exit-code mapping remains: `0` success, `64` client configuration/usage, `65` credential missing, `66` tenant access denied, `67` workspace locked, `68` idempotency conflict, `69` validation error, `70` known provider failure, `71` unknown provider outcome, `72` reconciliation required, `73` not found, `74` invalid state transition, `75` redacted, `1` internal error.
- MCP failure kinds remain one-to-one with canonical categories: `usage_error`, `credential_missing`, `tenant_access_denied`, `workspace_locked`, `idempotency_conflict`, `validation_error`, `provider_failure_known`, `provider_outcome_unknown`, `reconciliation_required`, `not_found`, `state_transition_invalid`, `redacted`, and `internal_error`.
- Latency is not a parity dimension. Authorization outcome, error category, idempotency behavior, audit metadata, correlation propagation, lifecycle state, and adapter-specific pre-SDK behavior are parity dimensions.

### Previous Story Intelligence

- Story 1.1 defines the scaffold shape, dependency direction, build constraints, and placeholder area for `tests/fixtures`.
- Story 1.2 defines root reproducibility and submodule policy. Do not use recursive nested-submodule initialization.
- Story 1.3 seeds the fixture/schema locations. This story may harden `parity-contract.schema.json` and `idempotency-encoding-corpus.json`, but it should preserve ownership notes and avoid generating final parity rows.
- Story 1.4 should produce C3 retention and C4 input-limit artifacts. This story consumes those decisions; if they are missing or unresolved, record a blocked prerequisite instead of inventing policy values.
- Recent story commits (`b6b4eef`, `6771d62`, `0e6aa9e`) show the recurring create-story flow keeps artifacts ready-for-dev and leaves implementation changes to later `dev-story` execution.

### Project Context Notes

- Target .NET remains `net10.0`; nullable, implicit usings, `LangVersion=latest`, and warnings-as-errors are inherited from root build configuration.
- The Contract Spine is OpenAPI 3.1 under `Hexalith.Folders.Contracts`; shared extension vocabulary includes `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, and `x-hexalith-sensitive-metadata-tier`.
- `Hexalith.Folders.Contracts` remains behavior-free. Rules documents and fixture schemas may describe future generation behavior, but generated helpers and runtime behavior belong to later stories.
- Authoritative tenant context comes from authentication context and EventStore envelopes, never request payload authority.
- Context-query authorization order is tenant, folder ACL, path policy, include/exclude rules, binary/large-file policy, range/result limits, then query execution.
- Never store raw provider tokens, secrets, file contents, diffs, generated context payloads, or unauthorized resource existence in rules, examples, fixtures, events, logs, traces, metrics, projections, audit records, diagnostics, or errors.

### Testing Guidance

- A focused documentation/fixture verification is enough for this story if the scaffold cannot yet host full xUnit checks.
- Prefer existing test infrastructure from the scaffold once available; do not add a new parser dependency solely for this story if a standard or already referenced parser can handle the check.
- Verification should prove required operation rows exist, mutating rows include idempotency-equivalence fields, query rows include read-consistency classes, required parity schema columns exist, and encoding-corpus categories are present.
- Verification should include negative examples for cross-tenant idempotency-key reuse, changed payloads, changed credential scope, missing or malformed idempotency keys, pre-SDK validation failures, and post-SDK/service failures.
- Verification should fail on missing AC9 table headings/columns or narrative-only rules for required metadata that later stories must consume mechanically.
- Build verification should use `dotnet build Hexalith.Folders.slnx` when the scaffold supports it. If not, record the precise missing scaffold prerequisite.
- Tests and scripts must not require Dapr sidecars, Aspire topology, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, or initialized nested submodules.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.5: Finalize idempotency equivalence and adapter parity rules`
- `_bmad-output/planning-artifacts/epics.md#Pre-Spine Workshop (Phase 0.5 - exit criteria deliverables)`
- `_bmad-output/planning-artifacts/epics.md#Story 1.6: Author Contract Spine foundation and shared extension vocabulary`
- `_bmad-output/planning-artifacts/architecture.md#Architecture Exit Criteria - Targets to Resolve`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#Spine Authoring Checklist`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Error Codes`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`
- `CLAUDE.md#Git Submodules`

## Project Structure Notes

- The primary deliverable is expected under `docs/contract/`. If the directory does not exist, create only that narrow documentation location.
- The fixture/schema files live under `tests/fixtures/` and were seeded by Story 1.3. This story may strengthen them but must not generate the final C13 oracle.
- This story is a contract-rules bridge between Phase 0.5 decisions and Story 1.6 OpenAPI foundation work. Treat unresolved C3/C4 or missing fixture seeds as prerequisite drift, not as permission to widen scope.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Implemented Story 1.5 idempotency and parity rules artifact, parity schema hardening, encoding corpus expansion, and focused verification. | Codex |
| 2026-05-11 | Advanced elicitation applied machine-checkable table structure, parser-policy classification, safe-denial read examples, and verification hardening. | Codex |
| 2026-05-10 | Party-mode review applied tenant-scoped equivalence, decision-record, fixture-validation, adapter-failure timing, and negative-scope guardrails. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T21:34:32.2115325+02:00
- Selected story key: `1-5-finalize-idempotency-equivalence-and-adapter-parity-rules`
- Command/skill invocation used: `/bmad-party-mode 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Tighten the story so it remains a normative rules and fixture/schema hardening story, not OpenAPI, SDK, CLI, MCP, REST, parity-row, or persistence implementation work.
  - Make the operation inventory acceptance-oriented with idempotency requirement, read-consistency expectation, task/correlation identity, parity surfaces, and deferred implementation owner.
  - Make tenant-scoped idempotency equivalence explicit and require negative examples for cross-tenant key reuse, changed payloads, changed credential scope, malformed/missing keys, and replay conflicts.
  - Clarify adapter parity around failure timing, including pre-SDK validation failures, SDK/client errors, server contract errors, credential/authentication failures, task lifecycle failures, CLI exit-code mapping, and MCP failure-kind mapping.
  - Strengthen fixture/schema validation and negative-scope verification so later C13 parity oracle work can consume deterministic artifacts without inheriting hidden implementation decisions.
- Changes applied:
  - Added AC8 for decision-record separation, tenant-scoped equivalence, and required negative examples.
  - Added tasks for decision-record content, acceptance-oriented operation inventory, non-equivalent replay rules, SDK/CLI/MCP outcome table, parity-schema failure categories, fixture rejection behavior, encoding corpus edge cases, and negative-scope verification.
  - Added Dev Notes for decision-record requirements, tenant-scoped equivalence, adapter-parity boundaries, and negative verification examples.
- Findings deferred:
  - Exact OpenAPI 3.1 path/schema/header/error-envelope shape remains Story 1.6+ work.
  - NSwag configuration, generated SDK methods, generated `ComputeIdempotencyHash()` helpers, CLI commands, MCP tools, REST handlers, and runtime idempotency persistence remain downstream implementation work.
  - Full parity rows and C13 oracle generation remain Story 1.13+ work.
  - Final C3/C4 Contract Spine mapping remains dependent on Phase 0.5 decision artifacts.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-11: Loaded BMad customization, project context, sprint status, and Story 1.5 before implementation.
- 2026-05-11: Confirmed clean git worktree before starting story implementation.
- 2026-05-11: Confirmed Story 1.3 fixture seeds and Story 1.4 C3/C4 exit-criteria artifacts exist; recorded C3/C4 as proposed workshop values where referenced.
- 2026-05-11: Added failing `ContractRulesArtifactTests` before implementation, then implemented rules/fixtures until the tests passed.
- 2026-05-11: Verified negative scope: no OpenAPI spine, generated parity rows, SDK output, REST/CLI/MCP implementation, CI workflow gate, or nested submodule initialization was added.
- 2026-05-11: Ran targeted tests, scaffold fixture tests, solution build, and full solution test suite successfully.

### Completion Notes List

- Created `docs/contract/idempotency-and-parity-rules.md` with a decision record, MVP operation inventory, Phase 1 inventory candidates, mutating-command equivalence rows, non-mutating read-consistency rows, adapter parity dimensions, adapter outcome mapping, deferred-owner rows, and verification coverage.
- Hardened `tests/fixtures/parity-contract.schema.json` for C13 transport and behavioral parity columns, bounded adapter names, bounded CLI exit codes, bounded MCP failure kinds, task/correlation/idempotency/credential sourcing fields, and ownership metadata.
- Expanded `tests/fixtures/idempotency-encoding-corpus.json` with synthetic metadata-only cases for NFC, NFD, NFKC, NFKD, zero-width joiner, casing, ULID casing, ordering, whitespace, null versus omitted values, percent encoding, malformed idempotency keys, and duplicate JSON keys.
- Added `ContractRulesArtifactTests` to parse and verify the rules document, parity schema, encoding corpus, and negative-scope guardrails.
- Validation passed: focused Story 1.5 tests, full `Hexalith.Folders.Testing.Tests`, `dotnet build Hexalith.Folders.slnx --no-restore`, and `dotnet test Hexalith.Folders.slnx --no-restore`.

### File List

- docs/contract/idempotency-and-parity-rules.md
- tests/fixtures/parity-contract.schema.json
- tests/fixtures/idempotency-encoding-corpus.json
- tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs
- _bmad-output/implementation-artifacts/1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Advanced Elicitation

- Date/time: 2026-05-11T03:03:55.2715395+02:00
- Selected story key: `1-5-finalize-idempotency-equivalence-and-adapter-parity-rules`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules`
- Batch 1 method names: Expert Panel Review; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix; Critique and Refine
- Reshuffled Batch 2 method names: Pre-mortem Analysis; First Principles Analysis; Red Team vs Blue Team; Expand or Contract for Audience; Self-Consistency Validation
- Findings summary:
  - Later OpenAPI and parity-oracle stories need stable table structure, not only prose, to consume operation metadata without reinterpretation.
  - Idempotency edge cases need explicit classification for duplicate keys, null versus omitted values, percent encoding, Unicode normalization, and casing-sensitive identifiers while keeping canonicalization implementation deferred.
  - Non-mutating safe-denial examples should cover existence leakage across tenant, folder, path, task, provider binding, and audit resources.
  - Verification should fail when required metadata exists only as narrative text or when Phase 1 candidates are not clearly marked as candidate or deferred.
- Changes applied:
  - Added AC9 for stable table headings and column names across operation inventory, equivalence, read consistency, adapter parity, deferred owner, and verification coverage.
  - Added task coverage for machine-checkable rules tables, parser-policy classification, safe-denial read examples, and AC9 verification.
  - Added Dev Notes requiring structured metadata tables and deferred decisions instead of invented values.
  - Updated the Change Log with the advanced-elicitation hardening pass.
- Findings deferred:
  - Exact OpenAPI path/schema/header shapes, generated helper behavior, and final parity-oracle rows remain downstream story work.
  - Parser-policy choices are documented expectations only; canonicalization implementation remains Story 1.12+ work.
  - Any operation metadata not settled by approved PRD, architecture, Story 1.3, or Story 1.4 sources must remain a deferred decision for the dev-story agent to surface.
- Final recommendation: ready-for-dev
