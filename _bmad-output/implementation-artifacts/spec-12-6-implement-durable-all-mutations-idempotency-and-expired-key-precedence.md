---
title: 'Story 12.6: Expired-key contract and cross-surface mapping'
type: 'feature'
created: '2026-09-06'
status: 'done'
baseline_commit: '7ec7adac3b41625a5243839a6d6c0dee41aec42d'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/implementation-artifacts/12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence.md'
  - 'docs/exit-criteria/oq8-idempotency-design.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore already emits `idempotency_key_expired`, but Folders has no matching canonical category, OpenAPI example, gateway mapping, CLI exit 76, or MCP kind. A gateway 409 from EventStore would be collapsed or treated as a generic denial.

**Approach:** Add `idempotency_key_expired` to the Contract Spine and project it unchanged through domain result codes, REST Problem Details, generated SDK, CLI, and MCP. Leave EventStore admission, MessageId/key identity, and Story 12.1 untouched.

## Decisions

- **Split:** This spec is only expired-key vocabulary and surface mapping. Admission wiring, adapters, DW-295/296, mutation-service reorder, and the durable OQ8 matrix are deferred (see `deferred-work.md`).
- **FOLDERS-NOW:** Implement that slice against pinned EventStore 3.102.0. Keep Story 12.6 and OQ8 **in-progress**. Do not mark Story 3.10 done.
- **FOLDERS-GATEWAY-ONLY:** Do not populate `SubmitCommandRequest.IdempotencyKey`, do not change `MessageId = idempotencyKey`, do not register `IIdempotencyIntentAdapter`, and do not edit `references/Hexalith.EventStore`.

## Boundaries & Constraints

**Always:** HTTP 409, category and code `idempotency_key_expired`, `retryable = false`, `clientAction = refresh_state_then_submit_with_new_key`, CLI exit 76, MCP kind `idempotency_key_expired`. Keep `idempotency_conflict` distinct. Regenerated client, helpers, and `parity-contract.yaml` stay in lockstep with OpenAPI. Hardcoded "47 categories" / CLI-exit tables must move with the new member. Problem details stay metadata-only.

**Never:** Send `IdempotencyKey` or change MessageId identity. Reference EventStore.Server from Folders. Implement Story 12.1, adapters, DW-295/296, service reorder, or the durable OQ8 matrix. Close OQ8 or Story 12.6. Hand-edit generated SDK/C13 rows. Log or return raw keys, fingerprints, payloads, paths, refs, or tokens. Rewrite read-route rejection (existing `idempotency_key_not_allowed` stays).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Gateway expired | EventStore 409 `idempotency_key_expired` (snake/kebab/Pascal) | Folders 409 same category/code, `retryable=false`, `clientAction=refresh_state_then_submit_with_new_key` | Do not map to conflict, missing, or 403 fallback |
| Domain expired | `FolderResultCode.IdempotencyKeyExpired` | Same canonical surface via `FolderCanonicalErrorMapper` | N/A |
| CLI expired | SDK `CanonicalErrorCategory.Idempotency_key_expired` | Exit 76 | Must not fall through to exit 1 |
| MCP expired | Same category | Failure kind `idempotency_key_expired` | Must not fall through to `internal_error` |
| Live conflict unchanged | EventStore 409 `idempotency_conflict` | Still 409 `idempotency_conflict`, CLI 68 | N/A |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- add enum member (after `idempotency_conflict` ~`:10116`), `CliExitCode` `"76"`, mutation `x-hexalith-canonical-error-categories`, and an `IdempotencyKeyExpiredGeneric` example mirroring `IdempotencyConflictGeneric` (`:5958-5971`).
- `tests/tools/parity-oracle-generator/Program.cs:1264` -- map `idempotency_key_expired` → CLI 76 / MCP `idempotency_key_expired`. `tests/fixtures/parity-contract.schema.json` -- allow the category, kind, and exit 76.
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` -- NSwag + helper generation on build; do not hand-edit `Generated/`.
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`, `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs` -- new result code; category `idempotency_key_expired`; status 409; not retryable; client action `refresh_state_then_submit_with_new_key`.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` -- `ToArchiveGatewayProblem` (`:3749-3757`), `SafeGatewayReasonCode` (`:3954-3956`), `MessageFor` (`:5761`). `SafeProblem` already reads client action from the mapper (`:5732-5733`).
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` -- preserve the new result code on the processor/problem path the way `IdempotencyConflict` is preserved.
- `src/Hexalith.Folders.Cli/FoldersExitCodes.cs`, `Errors/ErrorProjection.cs`, `src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs` -- exit 76 and MCP kind. Update the CLI remarks table that currently ends at 75.
- `docs/contract/idempotency-and-parity-rules.md`, `docs/operations/canonical-error-catalog.md` -- reflect generated vocabulary (today "exactly 47 members"); do not re-author runtime admission.
- Tests: `FolderCanonicalErrorMapperTests.cs`, `ErrorProjectionTests.cs`, `FailureKindProjectionTests.cs`, `ProviderErrorDocsConformanceTests.cs` (47 → 48), plus a Server test that a gateway expired reason is not collapsed.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- add category, exit 76, mutation category lists, and expired Problem Details example -- spine is the source of truth.
- [x] `tests/tools/parity-oracle-generator/Program.cs`, `tests/fixtures/parity-contract.schema.json` -- map and allow the new outcome -- generator fails closed on unmapped categories.
- [x] `docs/contract/idempotency-and-parity-rules.md`, `docs/operations/canonical-error-catalog.md` -- document expired mapping and the moved category count -- docs reflect contracts.
- [x] Build `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` and run the parity-oracle generator -- regenerate client/helpers/`tests/fixtures/parity-contract.yaml`; never hand-edit those outputs.
- [x] `FolderResultCode.cs`, `FolderCanonicalErrorMapper.cs`, `FolderDomainProcessor.cs`, `FoldersDomainServiceEndpoints.cs` -- domain + REST + gateway mapping -- EventStore 409 expired survives the hop.
- [x] `FoldersExitCodes.cs`, `ErrorProjection.cs`, `FailureKindProjection.cs` -- CLI 76 and MCP kind -- adapters do not fall through to internal_error.
- [x] Mapper, CLI, MCP, conformance, and gateway-mapping tests -- I/O matrix is executable.
- [x] `_bmad-output/implementation-artifacts/12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence.md` -- tick only mapping/contract subtasks; story and OQ8 stay in-progress.

**Acceptance Criteria:**
- Given EventStore returns 409 `idempotency_key_expired`, when Folders REST maps it, then the caller sees the same category/code, HTTP 409, `retryable=false`, and `clientAction=refresh_state_then_submit_with_new_key`, with no prior-intent fields.
- Given the generated SDK category `Idempotency_key_expired`, when CLI and MCP project it, then CLI exits 76 and MCP kind is `idempotency_key_expired`.
- Given a live `idempotency_conflict`, when the same surfaces run, then conflict mapping (409 / CLI 68 / MCP `idempotency_conflict`) is unchanged.
- Given this slice lands, when sprint/OQ8 status is inspected, then Story 12.6 and OQ8 remain in-progress and Story 3.10 is not marked done.

## Implementation Notes

- 2026-09-06: `FolderDomainProcessor.ToDomainResult` already rejects any non-success `FolderResultCode`, so `IdempotencyKeyExpired` needed no processor branch. Organization result codes were not extended (deferred with admission).
- 2026-09-06: Gateway mapping lives in shared `ToArchiveGatewayProblem`; expired is handled before the generic 409→conflict fallback. Verified: Server mapper+archive+prepare/bind 130/130; CLI ErrorProjection 49/49; MCP FailureKindProjection 56/56; ProviderErrorDocsConformance 20/20; `git diff --check` clean except unrelated `.bmad-loop/policy.toml` CRLF warning.
- 2026-09-06 review: ArchiveFolder expired theory now asserts RFC 9457 title/message; SDK quickstart and api-reference document expired 409 / CLI 76 / MCP kind and `refresh_state_then_submit_with_new_key`. Re-verified: Server 130/130; CLI ErrorProjection 49/49; MCP FailureKindProjection 56/56; ConsumerDocs+ProviderErrorDocs 42/42; `git diff --check` still only the unrelated policy.toml CRLF warning.

## Spec Change Log

## Review Triage Log

### 2026-09-06 — Review pass (iteration 0)

**Blind hunter**
- `medium` — Review diff includes unrelated dirty files: `.bmad-loop/policy.toml` `scm.isolation` `worktree` → `none`, rewritten `epic-3-context.md`, and untracked `bmad-build-auto-result-3-13-...`. Isolation `none` would let later loop runs mutate the checked-out branch. Not caused by expired-key mapping. **defer**.
- `false` — `FolderDomainProcessor.ToDomainResult` already `Rejection`s any non-success `FolderResultCode`, so `IdempotencyKeyExpired` needs no new branch. `OrganizationProviderBindingResultCode` has no expired member, so `MapOrganizationResultCode`'s default cannot collapse a produced expired outcome; ConfigureProviderBinding still uses `ToArchiveGatewayProblem` on the live gateway hop. Organization codes stay with deferred admission.
- `low` (rejected) — `components.responses.IdempotencyKeyExpired` is unused. OpenAPI allows one response per status; operations that `$ref` `IdempotencyConflict` already include `IdempotencyKeyExpiredGeneric`. Distinctness is the schema, example, and category lists, not a second 409 object.
- `low` (rejected) — CreateRepositoryBackedFolder/BindRepository 409 still `$ref` RepositoryConflict/DuplicateBinding. Those 409 slots already document other conflicts; runtime mapping is shared `ToArchiveGatewayProblem`. Mixing expired examples into those objects is more than a direct correction.
- `false` — Generated 409 `HexalithFoldersApiException` constructor strings come from the OpenAPI response description. CLI `CommandPipeline` projects `typed.Result.Category` and MCP uses the same enum; they do not key off the exception message.
- `false` — `CanonicalErrorCategory.Idempotency_key_expired` is a string `EnumMember`. Newtonsoft serializes the wire token; C# numeric values are not on the wire.
- `low` — `SafeProblem` titles every non-expired HTTP 409 `"Idempotency conflict."` Lock, duplicate-binding, repository-conflict, and reconciliation 409s already used that title before this slice; only expired is newly distinct. **defer** (pre-existing).
- `false` — EventStore `IdempotencyKeyExpiredExceptionHandler` sets `reasonCode` `idempotency_key_expired`. Gateway client tests prove `ReasonCode` is populated. Folders maps snake/kebab/Pascal aliases before the unmatched-409 fallback. The type URI is extra, not the discriminator this hop uses.
- `false` — Gateway mapping is shared `ToArchiveGatewayProblem`. `ArchiveFolderEndpointShouldMapGatewayExpiredKeyWithoutCollapsingToConflict` already covers snake/kebab/Pascal. Extra Prepare/Bind kebab copies would not exercise a second mapper.
- `medium` — `docs/sdk/quickstart.md` still documents only same-key-different-payload → `idempotency_conflict`. `docs/sdk/api-reference.md` still cites only conflict → 409 / CLI 68 and lists six `clientAction` tokens, omitting `refresh_state_then_submit_with_new_key`. Architecture's "43 post-SDK members" note is pre-existing illustrative prose that tells readers to trust the oracle. **patch** the two consumer SDK docs.
- `medium` — `docs/contract/idempotency-and-parity-rules.md` still omits `UpdateFolderAclEntry`, `ConfigureProviderBinding`, and `ConfigureBranchRefPolicy` from the inventory table. Frozen split left that expansion as remaining story work. **defer**.
- `false` — Frozen I/O is SDK category → CLI 76 / MCP kind, covered by `ErrorProjectionTests` and `FailureKindProjectionTests`. `CommandPipeline` reads `ProblemDetails.Category` from the typed body. Expired-equivalent vs expired-different indistinguishability is the deferred OQ8 matrix (no admission in this slice).
- `medium` — Generator fail-closed rules and `tests/fixtures/previous-spine.yaml` were not updated. Remaining story tasks; frozen Never/Split exclude completeness gates and the durable matrix. **defer**.
- `false` — Hardcoded verification `--repository-root` and empty change/triage logs are spec-file issues. Rejected: fix would be editing this build's spec (triage log is this pass).

**Edge-case hunter**
- `low` (rejected) — CreateRepositoryBackedFolder 409 `$ref`s RepositoryConflict only. Same as the blind Create/Bind 409 claim; runtime category still maps through the shared gateway helper.
- `low` (rejected) — BindRepository 409 `$ref`s DuplicateBinding only. Same as above.
- `medium` — `.bmad-loop/policy.toml` `scm.isolation` `worktree` → `none`. Unrelated to expired-key mapping; would make later loop runs mutate the checked-out branch. **defer**.

**Verification-gap**
- `medium` — `SafeProblem` title/message for expired 409s are unasserted. Archive/Prepare/Bind tests check category/code/retryable/clientAction only. Removing the title ternary and `MessageFor` arm would still pass those tests while CLI `ResultRenderer` printed a conflict title and the access-denied default message. **patch**.
- `medium` — Same `.bmad-loop/policy.toml` isolation flip. **defer**.
- `low` (rejected) — Unused `IdempotencyKeyExpired` response object. Same as the blind unused-response claim.

**Groups and routes** (no `intent_gap` / `bad_spec`; no loopback)
- **patch** (medium): REST expired title/message unasserted — extend the ArchiveFolder gateway theory.
- **patch** (medium): SDK quickstart and api-reference omit expired mapping and the new `clientAction`.
- **defer** (medium): unrelated dirty tree (`policy.toml` isolation, epic-3-context, auto-result artifact).
- **defer** (low): pre-existing all-409 `"Idempotency conflict."` title for non-idempotency conflicts.
- **defer** (medium): parity-rules inventory still a stale mutation subset.
- **defer** (medium): generator fail-closed / previous-spine snapshot still remaining Story 12.6 contract work.

## Design Notes

EventStore throws `IdempotencyKeyExpiredException` before aggregate work. Folders.Server is only a gateway client, so this slice teaches Folders to **repeat** that code rather than **decide** expiry. `IdempotencyKey` must stay unset: `AdmitAsync` runs only when the key is present, and a missing Folders adapter then becomes `idempotency_admission_unavailable` (503) for every mutation.

## Verification

**Commands:**
- `dotnet build src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj --configuration Release` -- expected: NSwag/helpers regenerate; 0 errors
- `dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root /home/administrator/projects/hexalith/folders` -- expected: oracle written; no `prerequisite_drift` for the new category
- Focused tests: `Hexalith.Folders.Server.Tests` mapper + gateway mapping; `Hexalith.Folders.Cli.Tests` ErrorProjection; `Hexalith.Folders.Mcp.Tests` FailureKindProjection; `Hexalith.Folders.Contracts.Tests` ProviderErrorDocsConformance -- expected: new rows green; report pre-existing reds without fixing them here
- `git diff --check` -- expected: no whitespace errors
