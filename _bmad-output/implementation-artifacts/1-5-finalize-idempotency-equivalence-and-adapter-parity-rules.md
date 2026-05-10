# Story 1.5: Finalize idempotency equivalence and adapter parity rules

Status: ready-for-dev

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

## Tasks / Subtasks

- [ ] Inspect prerequisites and preserve story boundaries. (AC: 1, 7)
  - [ ] Confirm Story 1.3 fixture seeds are present before changing `tests/fixtures/parity-contract.schema.json` or `tests/fixtures/idempotency-encoding-corpus.json`.
  - [ ] Confirm Story 1.4 Phase 0.5 deliverables exist, especially `docs/exit-criteria/c3-retention.md` and `docs/exit-criteria/c4-input-limits.md`, or record the exact missing prerequisite without fabricating values.
  - [ ] Do not initialize or update nested submodules; root-level submodules are read-only references unless the user explicitly asks for nested submodules.
  - [ ] Do not author the OpenAPI Contract Spine file in this story. Story 1.6 owns the foundation and shared extension vocabulary, and later Epic 1 stories own operation groups.
- [ ] Author the operation metadata rules artifact. (AC: 1, 2, 3)
  - [ ] Create `docs/contract/idempotency-and-parity-rules.md`, unless an existing contract-rules document clearly owns the same content.
  - [ ] Include the approved MVP command/query inventory from PRD command/query contract: `ValidateProviderReadiness`, `CreateFolder`, `BindRepository`, `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `AddFile`, `ChangeFile`, `RemoveFile`, `CommitWorkspace`, `GetWorkspaceStatus`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, and `GetAuditTrail`.
  - [ ] Add any additional operation names that Story 1.4 or the current architecture has made unavoidable for Phase 1, such as effective permissions, folder lifecycle/archive, provider support evidence, cleanup status, and operations-console projection queries. Mark them as "Phase 1 inventory candidate" if their exact OpenAPI operation ID remains for Story 1.6+ to freeze.
  - [ ] For each operation, classify command/query type, resource scope, authoritative tenant source, required task/correlation identity, lock requirement, expected state/projection family, canonical error categories, and audit metadata keys.
  - [ ] Keep operation IDs stable enough for Story 1.6+ to translate into OpenAPI, but do not create endpoint paths or schemas here unless needed to explain parity metadata.
- [ ] Define idempotency equivalence for every mutating command. (AC: 2)
  - [ ] For repository/folder creation or binding commands, include identity-defining fields only; exclude timestamps, generated operation IDs, status labels, diagnostic text, and projection-only fields from equivalence.
  - [ ] For workspace preparation, include tenant/folder/repository binding identity, task ID, branch/ref policy, provider binding reference, and workspace policy inputs needed to prevent duplicate workspaces.
  - [ ] For lock acquisition and release, include tenant/folder/workspace scope, task ID, lock intent, expected lock token or owner where applicable, and lease policy fields that affect behavior.
  - [ ] For file add/change/remove, include operation ID, path metadata, content hash or metadata reference, file-operation kind, task ID, workspace scope, and path-policy-relevant metadata; never include raw file content or diffs in rules or examples.
  - [ ] For commit, include task ID, workspace scope, branch/ref target, changed-path metadata digest, commit-message classification metadata, author metadata reference, and operation ID. Tie commit TTL to C3 retention rather than the 24-hour mutation tier.
  - [ ] State that fields are ordered lexicographically for `x-hexalith-idempotency-equivalence` and that consumers must use generated `ComputeIdempotencyHash()` helpers after Story 1.12 instead of hand-rolling canonicalization.
- [ ] Define read-consistency and parity dimensions for non-mutating operations. (AC: 3)
  - [ ] Assign read consistency classes using the architecture vocabulary: `snapshot-per-task`, `read-your-writes`, or `eventually-consistent`.
  - [ ] For context queries, include C4-derived bounds, path policy, include/exclude policy, binary/large-file policy, range limits, result limits, timeout behavior, truncation behavior, and included/excluded audit visibility.
  - [ ] For audit and timeline queries, require metadata-only responses and C3 retention awareness.
  - [ ] For status and operations-console projections, distinguish accepted command state from projected state when projection lag exists, and include freshness/correlation metadata.
  - [ ] Record that non-mutating operations do not accept `Idempotency-Key`; they still require correlation behavior and safe authorization-denial parity.
- [ ] Finalize adapter parity contract details. (AC: 4)
  - [ ] Copy the architecture Adapter Parity Contract into an implementation-facing section or reference it directly from `docs/contract/idempotency-and-parity-rules.md` with all required dimensions intact.
  - [ ] Preserve SDK behavior: caller-provided idempotency key or DI provider, no SDK auto-generation; correlation may be caller-provided or generated by SDK provider; task ID is caller-provided for task-scoped operations.
  - [ ] Preserve CLI behavior: `--idempotency-key` or `--allow-auto-key` for mutating commands, `--correlation-id`, required `--task-id` for task-scoped commands, credential precedence `HEXALITH_TOKEN` then `~/.hexalith/credentials.json` then `--token`, and canonical exit codes 0, 64-75, and 1.
  - [ ] Preserve MCP behavior: required `idempotencyKey` on mutating tool inputs, optional `correlationId`, required `taskId` for task-scoped tools, `auth.token` or `auth.tokenFile` configuration, and the canonical failure-kind set.
  - [ ] Assert that CLI/MCP wrapping the SDK does not erase behavioral parity dimensions; pre-SDK errors, post-SDK projection, idempotency-key sourcing, correlation sourcing, and credential sourcing remain testable.
- [ ] Harden parity schema and encoding fixtures only as far as this story requires. (AC: 5, 6)
  - [ ] Update `tests/fixtures/parity-contract.schema.json` only for the row-shape and required-column validation needed by C13. Do not generate `tests/fixtures/parity-contract.yaml`.
  - [ ] Update `tests/fixtures/idempotency-encoding-corpus.json` only with synthetic metadata-only cases needed by future `ComputeIdempotencyHash()` tests. Do not implement hash helpers.
  - [ ] Preserve any existing ownership notes from Story 1.3 and add notes that Story 1.12 and Story 1.13 own helper generation and oracle generation.
  - [ ] Ensure fixture changes contain no real tenant IDs, provider tokens, remote URLs with credentials, file contents, diffs, generated context payloads, production issuer URLs, or secrets.
- [ ] Add focused verification. (AC: 5, 6, 7)
  - [ ] Prefer a lightweight script, documentation test, or existing test project check that parses `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, and `tests/fixtures/idempotency-encoding-corpus.json`.
  - [ ] Verify that every mutating command has an idempotency-equivalence field list and every query has a read-consistency class.
  - [ ] Verify that required behavioral-parity columns are present in the parity schema.
  - [ ] Verify that the encoding corpus is parseable and contains the Unicode/casing categories required by this story.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` when the scaffold supports it. If build is blocked by prior scaffold work, record the exact prerequisite instead of expanding this story's scope.

## Dev Notes

### Scope Boundaries

- This story produces implementation-facing contract rules and fixture/schema hardening only.
- Do not author `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. Story 1.6 owns the OpenAPI foundation and extension vocabulary; Stories 1.7 through 1.11 own operation groups.
- Do not wire NSwag SDK generation, generate `ComputeIdempotencyHash()`, generate `tests/fixtures/parity-contract.yaml`, implement REST endpoints, implement CLI commands, implement MCP tools, implement SDK methods, implement domain aggregates, or add CI workflow gates.
- Do not change the architecture policy that `Hexalith.Folders.Contracts` remains behavior-free.
- Do not modify sibling submodules (`Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`). Use them only as read-only references.

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
- Equivalence lists are declarative and ordered lexicographically inside `x-hexalith-idempotency-equivalence`.
- Generated helpers after Story 1.12 own canonicalization. No adapter, caller, CLI command, MCP tool, or test should hand-roll the hash algorithm.
- Same key plus equivalent payload returns the same logical result.
- Same key plus different payload returns canonical `idempotency_conflict`.
- Mutation-tier TTL is 24 hours. Commit-tier TTL inherits C3 retention because commit reconciliation and audit reconstruction need longer evidence.
- Idempotency records must never store file contents, diffs, provider tokens, credential material, secrets, or unauthorized resource existence.

### Adapter Parity Rules

- SDK, REST, CLI, and MCP preserve the same canonical category, code, retryable flag, client action, correlation ID behavior, audit metadata, and terminal/projection state for equivalent inputs.
- SDK and REST own transport parity; CLI and MCP own behavioral parity dimensions that SDK wrapping does not normalize.
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
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
