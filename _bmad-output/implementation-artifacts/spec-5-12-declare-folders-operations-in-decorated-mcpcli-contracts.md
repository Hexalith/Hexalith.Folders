---
title: '5.12 Declare Folders operations in decorated McpCli Contracts'
type: 'feature'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_revision: 'd873861677982a737d131180bcf79f06caee034d'
baseline_commit: 'c5c8c19475188c6b8f8963a7b7051c34225867cf'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** McpCli cannot discover or validate any Folders operation. `Hexalith.Folders.Contracts` has no decorated Command/Query types, so every Folders inventory row stays `defer / exclude`, and McpCli Story 5.8 cannot re-disposition rows (inventory gap G1).

**Approach:** Declare each owner-eligible inventory candidate as one decorated public type in `Hexalith.Folders.Contracts`. Reference only `Hexalith.McpCli.Abstractions`, map each Command explicitly to its existing `Hexalith.Folders.Commands.*` wire name, and gate every type against the v2 Contract Spine. Record the owner's eligibility and behavior-loss decisions as the input to McpCli Story 5.8.

## Boundaries & Constraints

**Always:**
- Module: `[assembly: HexalithModule("folders", …, IdentifierKind.String)]` with the default `WireTypeConvention.Explicit`, no `FixedTenant` and no `SerializerOptionsProvider`. Folders IDs are opaque strings, not ULIDs.
- One public sealed record per file. Commands go in `Commands/`, queries in `Queries/`, and nested request objects and enums in `Commands/Models` or `Queries/Models`. Type names are the inventory CLR names exactly (`Contracts.Commands.CreateFolder`, not `CreateFolderCommand`).
- Every type and property gets a nonblank `Description`. Every `*Id`/`*Ref` identifier property gets `[HexalithIdentifier]`. Every property carries an explicit `[JsonPropertyName]` with the spine's camelCase name, because McpCli applies no naming policy. Enum members carry `[JsonStringEnumMemberName]` with the spine value. Spine bounds are declared with DataAnnotations (`StringLength`, `RegularExpression`, `Range`, `MaxLength`/`MinLength`); McpCli ignores them, but the drift gate checks them.
- Commands set `Domain = "folders"`, `WireType = "Hexalith.Folders.Commands.<Name>"`, `AggregateIdProperty = nameof(FolderId)` and `IdempotencyKeyProperty = nameof(IdempotencyKey)` (a required string). Fields come from the v2 request body plus path parameters. Task-scoped commands (catalog `task: true`) add a required `TaskId`.
- Queries set `Domain = "folders"`, `WireType = "Hexalith.Folders.Queries.<Name>"` and `ProjectionType` = the kebab operation part (e.g. `get-workspace-lock`). They take `AggregateIdProperty = nameof(FolderId)` when the spine scopes them to a folder, else `ProviderBindingRef`; with neither, no aggregate source is declared. Fields come from the path, query, body and `X-Hexalith-Task-Id` inputs. Paging inputs (`cursor`, `limit`, `filter`) are typed properties. Freshness is not a property, because each operation has one fixed value (V01).
- Spine mapping exceptions, pinned in the drift gate's exemption table:
  - `requestSchemaVersion` is implied by admission (Story 5.13).
  - C08 `GrantFolderAccess` and C13 `RevokeFolderAccess` each map to `UpdateFolderAclEntry`, with `effect` fixed to grant or revoke respectively (V08).
  - C10 `MutateFiles` maps to the `MutateFilesRequest` component.
  - `CreateFolder` adds a caller-supplied `FolderId` routing member, because the domain requires `payload.FolderId == envelope.AggregateId` (`FolderDomainProcessor.cs:374`).
  - C05 routes on `ProviderBindingRef`. Deriving the reserved `ProviderBindingActorId` and rejecting an actor equal to the tenant are a named gap owned by Story 5.13.

**Never:** No change to `src/` outside `Hexalith.Folders.Contracts`. No Folders CLI, MCP host or McpCli adapter code, no `Hexalith.EventStore` reference and no `ICommandContract`. No inline `Version=`, ProjectReference, or edit to the `references/` submodules. No C13 `mcpcli_operation` column (oracle work), no admission or handler change (5.13/5.14), and no publish or push.

## Decisions (owner, 2026-11-10, "apply recommended")

- **D1 — execution authorized, scoped.** DEC-EXEC-5.12 is authorized ahead of Story 1.17's closure. Build against the v2 spine released in Contracts 1.3.3 (oracle `contract_spine_sha256` `d417afd8…`). If 1.17 later changes the spine, the drift gate reddens and the types follow. Recording the manifest's `execution_authorization_status` is left to the next manifest reconciliation; this story does not edit `planning-story-manifest.yaml`. The `sprint-status.yaml` row follows the normal bmad-build status sync.
- **D2 — 41 eligible operations.**
  - **Eligible:** C01–C13 plus Q01–Q35, except Q04, Q06, Q10, Q12, Q15, Q17 and Q20 (13 Commands + 28 Queries).
  - **Excluded:** the 7 `operations_console_projection` diagnostics, which are seed-backed for MVP. They stay undecorated and inventory `defer`; each needs a product-approved C13 not-applicable declaration later (not this story).
  - **Consent:** decorating is the owner's consent to agent exposure without human confirmation, including C01 archive, C05 provider-binding configuration, C10 (with file removal) and C13 ACL revoke.
  - **Live-vector chain:** C06 `CreateFolder` → C08 `GrantFolderAccess` → Q29 `ListFolderAclEntries`.
- **D3 — behavior losses accepted** (the input to McpCli Story 5.8):
  - **V04:** local-file and stdin input and the upload helper's derived evidence. Replacement: the caller sends inline content plus evidence up to the inclusive 262144-byte boundary; larger content stays `defer` pending McpCli 5.2 staging.
  - **V07:** `--allow-auto-key` and legacy `sha256:` keys. Replacement: a caller-supplied uppercase ULID key.
  - **V09:** legacy credential precedence, human output and exit codes 64–77. Replacement: McpCli profiles, JSON output and exit 2.
  - **R01/R02:** the MCP resources. Replacement: queries Q28 `ListAuditTrail` and Q30 `ListFolderFiles`.
- **D4 — PD10:** add the new paths to `scripts/pd10-v2-story-owned-paths.txt` only. Do not regenerate the manifest; resealing stays with 12.2/1.17. Report `Pd10ConformanceSetTests` as red, both pre-existing and expected.
- **D5 — scope:** keep one spec (about 3,400 tokens, accepted over the 1,600 target).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Wire pin | each decorated Command | `WireType` equals the `FoldersCommandCatalog` entry `Hexalith.Folders.Commands.<Name>` | mismatch or missing entry fails the test |
| Spine drift | a field added, renamed or dropped, or a required flag or bound changed, on either side | the drift gate names the type, field and difference | fails unless the field is in the exemption table |
| ID round-trip | sample folder, workspace, task and provider-binding IDs in the v2 opaque grammar and the legacy grammar | STJ serialize/deserialize is byte-identical, case preserved | IDs McpCli rejects as aggregate IDs (an ID ending in `_`/`-`, or a legacy ID with a leading `.`/`_`/`-`) are asserted as the named gap list |
| Catalog shape | all decorated types | operation names unique after `KebabCase.FromTypeName`; no forbidden member types (object/JsonElement/polymorphic) | violation fails the test |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` -- add `<PackageReference Include="Hexalith.McpCli.Abstractions" />`. The version comes from Builds `HexalithMcpCliVersion` 2.0.1 (`references/Hexalith.Builds/Props/Directory.Packages.props:21,107`). The package is NuGet-only (there is no `HexalithMcpCliRoot`), so no ProjectReference (`ScaffoldContractTests:179`).
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml` -- the authoritative spine; read-only. Request schemas are camelCase with `additionalProperties: false`. IDs use `OpaqueIdentifier` (`:18761`: `^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$`).
- `src/Hexalith.Folders/Routing/FoldersCommandCatalog.cs` -- wire names (`:75`) and `task:` flags; read-only. Its snake_case field lists are idempotency descriptors, not the payload shape.
- `src/Hexalith.Folders.Contracts/FolderOwnerReadQuery.cs` -- the existing query-type naming precedent (`Hexalith.Folders.Queries.*`); leave unchanged.
- `references/Hexalith.McpCli/src/Hexalith.McpCli.Abstractions/*.cs` -- the attribute surface. `KebabCase.FromTypeName` is reusable in tests.
- `references/Hexalith.McpCli/src/Hexalith.McpCli.Core/Catalog/CatalogBuilder.cs:93-250`, `Routing/RoutingResolver.cs`, `Schema/SchemaDeriver.cs:226-331` -- the runtime rules the conformance test mirrors: the aggregate member must be a serialized string, the aggregate grammar is 1–256 `[A-Za-z0-9._-]` starting and ending alphanumeric, a query `WireType` may not contain `:`, and `ProjectionType` is kebab and at most 64 characters.
- `tests/Hexalith.Folders.Contracts.Tests/` -- xUnit v3, Shouldly and YamlDotNet; new tests go under `McpCli/`.
- `tests/Hexalith.Folders.Contracts.Tests/**/Pd10ConformanceSetTests*`, `scripts/pd10-v2-story-owned-paths.txt`, `scripts/generate-pd10-v2-conformance-set.py` -- the PD10 manifest coupling; do not run the generator (D4).
- `tests/Hexalith.Folders.Contracts.Tests/**/ClientGenerationTests.cs:337-372` -- restores a copy of the Contracts csproj; must stay green.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` -- add the versionless Abstractions reference -- the A-12 sole dependency.
- [x] `src/Hexalith.Folders.Contracts/FoldersModule.cs` -- the assembly `HexalithModule` declaration -- module identity.
- [x] `src/Hexalith.Folders.Contracts/Commands/*.cs` and `Commands/Models/*.cs` -- one decorated record for each of C01–C13, plus nested types and enums -- G1 commands.
- [x] `src/Hexalith.Folders.Contracts/Queries/*.cs` and `Queries/Models/*.cs` -- one decorated record for each of the 28 eligible Q-candidates (D2); never decorate Q04, Q06, Q10, Q12, Q15, Q17 or Q20 -- G1 queries.
- [x] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersWireNameMappingTests.cs` -- pin every Command wire name against `FoldersCommandCatalog` and the inventory table -- AC wire mapping.
- [x] `tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs` -- compare fields, required flags, bounds and enum sets to the v2 spine, with one explicit exemption table -- AC drift gate.
- [x] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersIdentifierRoundTripTests.cs` and `McpCliCatalogConformanceTests.cs` -- the ID matrix rows and the catalog-shape rules. Pin each operation's routing strings (`Domain`, `WireType`, `ProjectionType` and aggregate source), and assert that the eligible set equals the decorated set exactly. Folders owns the per-operation routing-string and wire-type tests (McpCli Story 5.12) -- AC identifiers and AC eligibility.
- [x] `tests/Hexalith.Folders.Contracts.Tests/McpCli/PackedContractsClosureTests.cs` -- pack Contracts in Release; the nuspec's only dependency is `Hexalith.McpCli.Abstractions`, with no framework reference other than `Microsoft.NETCore.App` -- AD-15.
- [x] `docs/contract/mcpcli-folders-operation-declarations.md` -- one row per candidate for all 48 (eligibility per D2 with the exclusion reason, CLR type, wire name, spine operation, aggregate, exemptions), the D1 authorization, the D2 consent and live-vector chain, the D3 losses (rationale, replacement, affected rows), and the named gaps -- the input to McpCli Story 5.8.
- [x] `scripts/pd10-v2-story-owned-paths.txt` -- add every new `src/` and test path, sorted and unique; do not regenerate the manifest (D4) -- PD10 coverage.

**Acceptance Criteria:**
- Given a Release build with `TreatWarningsAsErrors`, when the solution builds, then MCPCLI001 reports nothing and every existing Contracts.Tests test outside the known pre-existing failures stays green.
- Given the decision record, when it is compared with the decorated assembly, then the eligible set and the decorated set are equal, and each behavior loss names its rationale, replacement and affected rows.

### Review Findings

Code review 2026-11-10 (bmad-code-review, full mode) of `c5c8c194..a9c77c85` (82 files, +3,478/−14). Four layers ran (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) and none failed. The 59 raw findings were checked against McpCli Core source (`CatalogBuilder`, `SchemaDeriver`, `OperationExecutor`, `DescriptionLinter`, `verify_artifact.py`), the v2 spine, `FolderDomainProcessor` and the retained `/tmp/folders-5-12-*` logs. Result: 3 decision-needed (all resolved as patches), 16 patch, 2 defer, 21 rejected.

- [x] [Review][Decision] Fixed ACL `effect` is unconstrained at the McpCli boundary — `GrantFolderAccess.Effect`/`RevokeFolderAccess.Effect` are optional free-form strings in the exported schema (real Core probe: `"effect":{"type":"string"}`, not required). `PayloadValidator` therefore admits `"effect":"revoke"` on `folders.grant-folder-access`, and `OperationExecutor` forwards the raw JSON. The typed `private init` silently coerces conflicting values, and the drift gate pins `typeof(string)`. The domain already takes intent from the command type (`FolderDomainProcessor.cs:1710` comment). Options: (1) drop `effect` from both payloads and keep the C08/C13 exemption as "derived from operation identity", so `additionalProperties:false` rejects any caller `effect`; this reverses the earlier Blind-1 patch. (2) Use a single-member enum per operation (`grant`/`revoke`), so the schema advertises the value and rejects conflicts; this adds 2 public enums. (3) Keep it as is and rely on the Story 5.13 E2 enforcement. Sources: blind+acceptance+edge. **Resolved 2026-11-10 (Jerome): option 1, drop `effect` → patch below.**
- [x] [Review][Decision] Command task/idempotency header inputs are synthesized instead of derived from the spine — the spine gives the 7 non-task Commands (C01, C02, C04, C05, C06, C07, C08/C13) an optional `X-Hexalith-Task-Id`. No record carries it, and the losses table does not record dropping optional task association. `ExpectedFields` (`ContractSpineDriftGateTests.cs:225-258`) skips every Command header and builds `idempotencyKey`/`taskId` from `OpaqueIdentifier` and the catalog flag. A change to the `Idempotency-Key` or task-header schema or required flag never reddens the gate, and the synthesized `taskId` overwrites the required `CommitWorkspaceRequest.taskId` body schema before comparison. Options: (1) add an optional `TaskId` to the 7 Commands and derive all header fields from spine parameters; this expands the spec's field rule. (2) Record optional task association as an accepted loss, or route it through McpCli `extensions["taskId"]` (inventory G3, Story 5.13), and make the gate derive the header fields with explicit exemption keys. Sources: blind+edge+acceptance. **Resolved 2026-11-10 (Jerome): option 2, route through McpCli `extensions["taskId"]` → patch below.**
- [x] [Review][Decision] The new McpCli tests cannot turn any CI lane red while PD10 keeps the lanes red — `run-contract-spine-gates.ps1` runs the OpenApi selector first (5 deterministic PD10 failures), and its aggregate exit keeps the first non-zero code. `ci.yml` runs the whole Contracts.Tests project. A McpCli drift, eligibility or closure regression changes no lane status. Options: (1) run the McpCli selector as its own `contract-spine.yml` step now; this touches PD10-bound CI files again, which the reseal absorbs. (2) Defer to the 12.2/1.17 PD10 reseal and record that until then the McpCli gates are log-only. Source: verification-gap (pre-verified). **Resolved 2026-11-10 (Jerome): option 1, separate step now → patch below.**
- [x] [Review][Patch] Drop the fixed ACL `effect` member from GrantFolderAccess and RevokeFolderAccess (decision 1) [src/Hexalith.Folders.Contracts/Commands/GrantFolderAccess.cs:31] — remove `Effect` from both records. Restate exemptions `C08/effect` and `C13/effect` as "derived from operation identity, not a payload member". The drift gate should expect the spine `effect` to be absent from the CLR shape, and a test should assert that the canonical options reject a caller-supplied `effect` (`UnmappedMemberHandling.Disallow`). Update decision-record E2: Story 5.13 supplies `effect` from the wire type when it translates to `UpdateFolderAclEntry`. Note in the Review Triage Log that this reverses the earlier Blind-1 serialization patch.
- [x] [Review][Patch] Derive Command header inputs from the spine and route optional task association through extensions (decision 2) [tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs:225] — build `idempotencyKey` from the resolved `Idempotency-Key` parameter (schema plus required flag). For task-scoped Commands, build `taskId` from the `X-Hexalith-Task-Id` parameter and check that it is required. Compare the `CommitWorkspaceRequest.taskId` body schema with the header instead of overwriting it. Add explicit exemption keys for the optional task header on C01, C02, C04, C05, C06, C07, C08 and C13: "carried by McpCli `extensions[\"taskId\"]`; Story 5.13/G3". The decision record should state that optional task association for non-task Commands travels in the envelope extensions, owned by 5.13.
- [x] [Review][Patch] Run the McpCli selector as its own contract-spine CI step (decision 3) [.github/workflows/contract-spine.yml:27] — move the McpCli namespace selector out of the aggregate `run-contract-spine-gates.ps1` run into a separate workflow step, so its pass/fail shows independently of the PD10-red OpenApi set. Update `ContractSpineCiGateTests` and, together with the doc patch below, `contract-spine-ci-gates.md`. List the changed PD10-bound rows under the PD10-impact patch.
- [x] [Review][Patch] The consent gate enumerates only exported types, while McpCli catalogs every visibility [tests/Hexalith.Folders.Contracts.Tests/McpCli/McpCliContractTestSupport.cs:84] — `CatalogBuilder` uses `assembly.GetTypes()` with no visibility filter (`CatalogBuilder.cs:65,84-92`). An `internal` decorated diagnostic would be catalogued while `EligibleInventoryAndDecisionRecordEqualTheDecoratedSetExactly` stays green. Use `GetTypes()` and assert that every decorated type is public. Sources: verification-gap+edge.
- [x] [Review][Patch] The canonical payload options mirror is unpinned, and nothing exercises null omission at runtime [tests/Hexalith.Folders.Contracts.Tests/McpCli/McpCliContractTestSupport.cs:111] — `CanonicalPayloadResolverMatchesTheMcpCliSource` compares only `CreateResolver()`. The `WhenWritingNull`/`Disallow`/naming/enum-converter settings that the drift gate's omission rule depends on are hand-copied from `McpCliJson.CreatePayload`. Extend the source comparison to `CreatePayload`, and assert that a default optional member (such as `CreateFolder.ParentFolderId`) is absent from canonical output. Sources: verification-gap+edge.
- [x] [Review][Patch] The decision record omits the payload-translation gap [docs/contract/mcpcli-folders-operation-declarations.md:126] — the decorated v2 request shapes differ from the domain payload records under the same wire types. `ArchiveFolderPayload` requires `requestSchemaVersion`. `CreateRepositoryBackedFolderPayload` requires `RepositoryBindingId`/`CredentialScopeClass`. `FolderAccessMutationPayload` expects `operations[{principalKind,principalId,action}]`. `FileMutationPayload` is a single-file root shape (`FolderDomainProcessor.cs:1693-1815`). The record's G2/G3 names only the request-schema version, conditional file rules, duplicates and byte limits. Add a 5.13-owned named gap listing each Command whose payload must be translated. Sources: acceptance+edge.
- [x] [Review][Patch] The provider-binding reserved-actor gap is named for C05 only [docs/contract/mcpcli-folders-operation-declarations.md:132] — Q13 `GetProviderBinding` and Q35 `ValidateProviderReadiness` also route on the caller-supplied `ProviderBindingRef`. Name the same derivation and tenant/namespace-separation requirement for the Story 5.14 query handlers. Source: acceptance.
- [x] [Review][Patch] The decision-record-to-assembly comparison is substring-only [tests/Hexalith.Folders.Contracts.Tests/McpCli/McpCliCatalogConformanceTests.cs:26] — `ShouldContain("eligible")` also matches "ineligible". The wire-name, spine-operation, aggregate-source and exemption columns, and the header `contract_spine_sha256`, are never compared with the declarations or the spine. Parse the columns, match eligibility exactly, and pin the spine digest. Sources: blind+edge+acceptance.
- [x] [Review][Patch] McpCli enrollment lint will flag 8 `payload_paging_member` findings that the record never lists [docs/contract/mcpcli-folders-operation-declarations.md:105] — `DescriptionLinter` warns on the query `Cursor` member of Q16, Q27, Q28, Q29, Q30, Q31, Q33 and Q34 (confirmed by the real Core probe). McpCli `verify_artifact.py check_lint` requires an exact `approvalReference` for each one. Record the 8 expected findings with V02 (paging stays typed payload) as the owner rationale for enrollment. Sources: blind+acceptance.
- [x] [Review][Patch] Agent-facing descriptions carry spine authoring artifacts [src/Hexalith.Folders.Contracts/Queries/GlobFolderFiles.cs:27] — the whole description of `GlobFolderFiles.Limit` and `SearchFolderFiles.Limit` is `TODO(reference-pending): docs/exit-criteria/c4-input-limits.md PM approval state is proposed.`, and the same text is appended on `GetFolderFileMetadata.Paths`, `ReadFileRange.EndOffset` and `SearchFolderIndexedFiles.Limit`. C4 has been approved since 2026-06-22. `FileMutationRequest.PathMetadata` and `ReadFileRange.Path` end with a literal `\n`. Rewrite the Contracts descriptions; the read-only spine keeps its own text. Sources: blind+acceptance.
- [x] [Review][Patch] Spine rules the declarations cannot express have no named owner [src/Hexalith.Folders.Contracts/Queries/ReadFileRange.cs:25] — the ReadFileRange derived rule (`endOffset >= startOffset`, window ≤ 262144, 400/422 outcomes) and `CommitWorkspaceRequest.auditMetadataKeys` `uniqueItems: true` (`CommitWorkspace.cs:37`) are neither declared, checked by the gate, nor assigned in G2/G3 or G4/G5. McpCli validation admits duplicates and reversed ranges. Name them under 5.13 (uniqueness) and 5.14 (range window). Sources: blind+edge.
- [x] [Review][Patch] The aggregate-grammar gap list misses legacy IDs with a trailing `.`/`_`/`-` [tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersIdentifierRoundTripTests.cs:169] — the legacy grammar `\A[a-z0-9._-]+\z` (`FolderCommandValidator.cs:964`) admits them, and `RoutingResolver.IsAggregateId` rejects them (its last character must be alphanumeric). Add the three samples to the gap dictionary, the theory data and the decision-record table. Source: edge.
- [x] [Review][Patch] The decision record understates the PD10 impact [docs/contract/mcpcli-folders-operation-declarations.md:181] — `contract-spine.yml`, `run-contract-spine-gates.ps1` and `OpenApi/ContractSpineCiGateTests.cs` are SHA-bound rows that this story changed. `Pd10V2ContractTests` now fails first on `contract-spine.yml` (3205→3232) instead of `Hexalith.Folders.Cli.csproj`, so "the same five failures" holds by test name only. Name the three changed rows in the reseal scope. Sources: acceptance+blind+edge.
- [x] [Review][Patch] The excluded-diagnostic rows give their aggregate source as `none` [docs/contract/mcpcli-folders-operation-declarations.md:66] — the spine scopes Q04, Q06, Q10, Q12, Q15, Q17 and Q20 by a required `{folderId}`. Show them as undecorated with spine scope `folderId`. Source: acceptance.
- [x] [Review][Patch] `contract-spine-ci-gates.md` was not updated for the McpCli selector [docs/contract/contract-spine-ci-gates.md:15] — it still says the script runs only "the focused OpenAPI gate tests and generated-client consistency tests". Its Gate Inputs table omits the McpCli inventory and `McpCliJson.cs` mirror that the new namespace reads from `references/Hexalith.McpCli`. Append to the existing sentence without rewording it, because `ContractSpineCiGateTests` reads this doc. Sources: blind+acceptance+edge.
- [x] [Review][Patch] Generated declarations have 29 unused `using` directives [src/Hexalith.Folders.Contracts/Queries/GetAuditRecord.cs:5] — 25 Command/Query files import their `*.Models` namespace without using it, and `FolderMetadata`, `PutFileInline` and both `PathMetadata` copies import `Hexalith.McpCli.Abstractions` without using it. Source: blind.
- [x] [Review][Defer] No automated path runs the McpCli catalog builder, `SchemaDeriver` or `DescriptionLinter` over Folders.Contracts [tests/Hexalith.Folders.Contracts.Tests/McpCli/McpCliCatalogConformanceTests.cs:1] — deferred: McpCli Core is not a consumable package, and the spec's Design Notes assign runtime catalog gates to McpCli enrollment (6.1). "41 operations, zero diagnostics" rests on the one-off `/tmp/folders-5-12-review-real-core-probe.log`.
- [x] [Review][Defer] Spine `x-hexalith-sensitive-metadata-tier: tenant_sensitive` request annotations cannot be carried by the declarations [src/Hexalith.Folders.Contracts/Commands/Models/BranchRefPolicyRequest.cs:1] — deferred (unverified, medium if true): `Hexalith.McpCli.Abstractions` has no sensitivity marker. To settle it, check whether McpCli heads echo or log request payload members such as `defaultRef`, `displayName` and `commitMessageClassification`. If they do, request a McpCli redaction capability.

**Rejected (21):**
- Blind 1, false: the record already states that McpCli ignores DataAnnotations and that 5.13/5.14 must reject explicit nulls; the probe-not-committed part is the first defer.
- Blind 3, low: boilerplate descriptions and no `Example`. The McpCli linter does not flag them (the probe's lint shows only paging), and rewriting about 100 descriptions is authoring, not a correction.
- Blind 4 and Acceptance 7 (key half), low: the payload `idempotencyKey` is `readOnly` and McpCli overwrites it from the envelope key, whose argument documents the uppercase-ULID rule (`OperationExecutor.cs:169,212`; `SchemaDeriver.cs:338-342`).
- Blind 6, false: the remaining special cases are spec mapping rules or pins (byteLength nullability, ContentBytes encoding, the query task header, AddFile scope under `C10/$request`), not exemptions. The `DefaultRef` `!` fails loudly.
- Blind 8, low: `HexalithIdentifier` on `DefaultRef` only forces the type and removes a pattern McpCli never exports, so there is no observable harm.
- Blind 10, low: the 5 extra catalog commands are outside the McpCli-owned 48-row inventory (organization ACL is N03, maintenance is N01). Their disposition belongs to the McpCli 5.8 inventory revision.
- Blind 13, low: the closure pins are intentional evidence. A Builds bump that reddens loudly is acceptable, and the pack and timeout nits are unlikely to matter.
- Blind 14, low: `McpCliJson.cs` is unchanged since v2.0.1, and linking the submodule pin to the package version adds machinery.
- Blind 15 (ADR part), low: main CI initializes every root submodule, and changing the 8-module command cascades through about 27 pinned docs.
- Blind 16, low: "accepted payload spellings" refers to serialization round-trip; this is a wording nit.
- Blind 19 (duplicate `PathMetadata`), low: the spec's Commands/Models versus Queries/Models placement requires both copies, and the drift gate covers both.
- Blind 20, low: "C13" for both the criterion and the candidate is established project vocabulary; the spec-claim part would edit the spec.
- Edge 1, low: a hypothetical JSON-name swap would affect only typed C# callers, because McpCli forwards raw JSON.
- Edge 2, low: no current CLR enum lacks a spine vocabulary; this guards against hypothetical future drift.
- Edge 5, low: no `[JsonInclude]` field or non-public serialized member exists besides the exempted `effect`.
- Edge 8 and 9, false: a NullReferenceException or InvalidOperationException in a test gate is a loud failure.
- Edge 12, false: McpCli validates against the exported schema, where a required string is non-null, and forwards raw JSON without reserializing.
- Edge 13, false: in McpCli, the options-level `JsonStringEnumConverter(allowIntegerValues:false)` outranks the type-level attribute, and these types have no other consumer.
- Edge 17 and Acceptance 8, low: the hand-copied grammar matches `RoutingResolver.IsAggregateId` today, and no sample has a trailing newline; pinning it to the source adds machinery.
- Edge 19, low: the canonical name derives from the CLR name, which is already pinned to the inventory.
- Edge 22, low: the substring check is weak, but the token appears only in the init step today.

### Review Findings — follow-up review (2026-11-11)

Code review 2026-11-11 (bmad-code-review, full mode) of the review-patch work `a9c77c85..976be351` (49 files, +571/−149). Four layers ran (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) and none failed. Verification Gap reported no gaps. The 32 raw findings were checked against the McpCli Core source (`ExtensionValidator`, `SettingsResolver`, `SchemaDeriver`, `DescriptionLinter`), `FolderCommandValidator`, `Pd10V2CandidateCompatibilitySeam`, the v2 spine and the workflow. Result: 1 decision-needed (resolved → patch), 10 patch, 1 defer, 13 rejected; 19 raw findings were grouped into the 11 surviving entries.

- [x] [Review][Decision] E6 optional task association depends on a McpCli client allowlist and loses valid IDs — E6 says "Story 5.13/G3 owns the extension allowlist" (`docs/contract/mcpcli-folders-operation-declarations.md:53`), and the record says E6 "preserves optional association rather than accepting its loss" (`:114`). Neither holds. McpCli enforces the allowlist on the client, from the selected profile's `allowedExtensions`, which is empty by default (`SettingsResolver.cs:118-132`, README "only the selected Profile supplies the extension allowlist"). `ExtensionValidator.Validate` refuses any unlisted key before dispatch (`ExtensionValidator.cs:37-40`, called at `OperationExecutor.cs:192`). Every McpCli caller must therefore run `config set <profile> allowedExtensions taskId` before a non-task Command can carry task association, and no story owns that setup. Extension values also pass McpCli's injection filter `--\s*$` (`ExtensionValidator.cs:95`). The filter rejects valid v2 task IDs ending in `--`, which the OpaqueIdentifier pattern `^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$` admits. Extensions are also absent from the operation schema, so agents cannot discover the association. Options: (1) keep the extensions route, name the profile allowlist prerequisite with an owner (McpCli 5.8 enrollment and Folders 5.10/5.11 parity setup), add the `--`-suffix task IDs as a named gap, and drop the "preserves" claim. (2) Switch to the earlier option 1: add an optional `TaskId` payload member to C01, C02, C04, C05, C06, C07, C08 and C13, derived from the spine header and compared by the drift gate, and remove E6. This adds 8 public members but needs no profile setup and loses no IDs. (3) Record optional task association on non-task Commands as an accepted behavior loss in the D3 table. Sources: blind+acceptance. **Resolved 2026-11-11 (Jerome): option B, keep extensions and correct the record → patch below.** Further evidence: `FolderDomainProcessor.ProcessCoreAsync` already rejects every `/process` Command without a canonical `extensions["taskId"]` (`FolderDomainProcessor.cs:128`, `:1444-1450`). The Folders REST path already submits task identity that way (`FoldersGatewayHumanBearerHandler.cs:44`). The EventStore Gateway applies the same `--\s*$` rejection to every extension (`ExtensionMetadataSanitizer.cs:126`). So the extension is the platform-native carrier, the profile allowlist is a prerequisite for all 13 Commands, and the `--` limit affects every caller, not McpCli alone.
- [x] [Review][Patch] Correct E6 and the named gaps to state the real task-extension contract (decision above) [docs/contract/mcpcli-folders-operation-declarations.md:51] — (a) the current domain requires a canonical `extensions["taskId"]` on every Gateway-dispatched Command, so it is the platform-native task carrier and not a 5.12 invention; (b) McpCli refuses the key until the caller profile allowlists `taskId` (`config set <profile> allowedExtensions taskId`, empty by default), which is a prerequisite for all 13 Commands owned by McpCli enrollment (5.8/6.1 vectors) and the Folders 5.10/5.11 parity setup, not by 5.13; (c) extension values ending in `--` are rejected by both McpCli and the Gateway for every caller, REST included, so they are a platform-wide named gap and not a McpCli behavior loss; (d) Story 5.13 reconciles payload `TaskId` with the extension for the task-scoped C03, C09, C10, C11 and C12 and validates the value grammar; (e) replace "This preserves optional association rather than accepting its loss" (`:114`) with "preserved when the caller profile allowlists `taskId`". Docs only; keep the E6 code and exemption keys.
- [x] [Review][Patch] The translation handoff tells 5.13 to supply `requestSchemaVersion: v2`, but the existing domain accepts only `v1` [docs/contract/mcpcli-folders-operation-declarations.md:159] — `FolderCommandValidator.cs:110,123,139,154,172,186,199` reject any value other than `"v1"`, and the v2 REST path rewrites `v2`→`v1` at the root and on the nested `branchRefPolicy` before dispatch (`Pd10V2CandidateCompatibilitySeam.cs:1286-1300`). Restate the requirement: supply each domain payload's required discriminator at the value the domain accepts (today `v1`, mirroring that seam). Source: acceptance.
- [x] [Review][Patch] `CommandHeaderAndDuplicateBodyDriftCannotBeHidden` accepts any exception as proof of detection [tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs:162] — `Record.Exception(...)` followed by `ShouldNotBeNull` also passes on a `NullReferenceException`, a `Single()` failure or a broken fixture. All 9 cases fail for the intended reason today, so the gap is latent. Assert `ShouldAssertException`, and assert that the message names the mutated field (`idempotencyKey`/`task`/`taskId`), as the sibling allOf mutation test does. Sources: blind+acceptance.
- [x] [Review][Patch] `DecisionExemptionCodes` reads the C10 schema raw instead of resolving it like `ExpectedFields` [tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs:410] — the build follow-up fixed this C10 root-`allOf` bypass in `ExpectedFields` but not here. E1 is detected only from direct `properties`, so moving `requestSchemaVersion` into an `allOf` member would make the computed codes disagree with the gate. Wrap the C10 body in `McpCliContractTestSupport.Resolve`. Today `MutateFilesRequest` declares the discriminator directly, and no decorated operation has a nested-only discriminator. Sources: blind+edge+acceptance.
- [x] [Review][Patch] E2 names the wrong rejection mechanism for a caller-supplied `effect` [docs/contract/mcpcli-folders-operation-declarations.md:41] — McpCli never deserializes the payload. `PayloadValidator` validates the raw JSON against the `SchemaDeriver` schema, which closes objects with `additionalProperties: false` (`SchemaDeriver.cs:348`), and then forwards it. The local test only proves the STJ mirror's `UnmappedMemberHandling.Disallow`. Name the closed exported schema as McpCli's boundary, keep the test as mirror evidence, and note that callers who reach the Gateway directly are still 5.13's concern. Source: blind.
- [x] [Review][Patch] `SearchFolderIndexedFiles.Limit` still shows the internal "C4 result bound" wording to agents and never states the maximum [src/Hexalith.Folders.Contracts/Queries/SearchFolderIndexedFiles.cs:30] — McpCli ignores DataAnnotations, so `Range(1, 500)` never reaches the exported schema. Rewrite the XML `<param>` and `Description`, for example "Maximum number of indexed file results, from 1 through 500; defaults to 500 when omitted." Sources: blind+acceptance.
- [x] [Review][Patch] The new McpCli CI step has no `timeout-minutes` [.github/workflows/contract-spine.yml:47] — every subsequent gate step sets `timeout-minutes: 15`, and the job has no job-level timeout, so a hung test host would run to the 360-minute default. The pack subprocess has its own 2-minute kill, but the xUnit host does not. Add `timeout-minutes: 15`. This file is already in the PD10 reseal scope. Sources: blind+edge.
- [x] [Review][Patch] The inserted McpCli sentence changes the antecedent of "It is validation-mode only" [docs/contract/contract-spine-ci-gates.md:16] — "It" now reads as the McpCli selector instead of the script. Move the McpCli sentence after "…must leave tracked generated output unchanged." Both substrings that `ContractSpineCiGateTests` reads stay intact. Sources: blind+acceptance.
- [x] [Review][Patch] The `sprint-status.yaml` header rewrite dropped the 2026-11-04 Story 12.3 chunk-1 history entry [_bmad-output/implementation-artifacts/sprint-status.yaml:4] — the chain now jumps from the 2026-11-10 correct-course entry to `previous_last_updated` 2026-10-23 (Story 11.5). Restore the nested `Previous: 2026-11-04 (Story 12.3 code review chunk 1 of 3 …)` text from `a9c77c85` inside the correct-course entry. Source: blind.
- [x] [Review][Patch] The 13-row command-payload-translation table is separated from its bullet [docs/contract/mcpcli-folders-operation-declarations.md:182] — it sits after the G4/G5, G6 and staged-content bullets, and it splits the named-gaps list in two before `identifier-aggregate-grammar`. Move the intro sentence and table directly under the `command-payload-translation` bullet, or into their own subsection after the list. The `^\| [CQ][0-9]{2} \|` row parser does not match these rows, so moving them is test-neutral. Source: blind.
- [x] [Review][Defer] The new 5.13/5.14 handoff obligations are not reflected in the Story 5.13/5.14 acceptance criteria [_bmad-output/planning-artifacts/epics.md:2258] — deferred: the fix edits other planning specs. Story 5.13 does not name the translation of all 13 Command payloads, wire-type `effect` derivation, discriminator supply or audit-key uniqueness. Story 5.14 does not name the Q13/Q35 reserved-actor derivation or the ReadFileRange window. Neither story cites `docs/contract/mcpcli-folders-operation-declarations.md`. Both are backlog; carry these items in when their specs are created. Source: blind.

**Rejected (13):**
- Blind 1, false: queries route on `ProviderBindingRef` by the spec's query rule, which needs no exemption (only the Command rule fixes `FolderId`), and the named gap now gives Q13/Q35 to Story 5.14.
- Blind 2 and Edge 1, low: the McpCli lane pins the spine's SHA-256 (`McpCliCatalogConformanceTests`), so any added or changed header turns it red. Correlation is envelope-carried and freshness is V01. Per-header exemption keys would add machinery.
- Blind 7, low: runtime catalog acceptance is already deferred to McpCli 6.1 (`deferred-work.md:3236-3245`). Removing a member cannot introduce a catalog or lint diagnostic, and `/tmp` evidence logs are the established project convention.
- Blind 8 and Acceptance 4, low: McpCli enrollment `check_lint` requires an exact approved set and fails loudly on a stale V02 row. The 8 rows match the 8 queries that declare `cursor` today. The translation table is prose handoff.
- Blind 10, low: a missing test assembly still fails the step loudly with a dotnet error. Re-adding `Test-Path` and a local zero-test guard, or pinning the doc command, would add guards.
- Blind 16, low: the third ledger entry deliberately supplements the first, under the same owner, as the build follow-up triage directed. The first entry's probe sentence accurately describes the evidence that exists.
- Edge 2, low: a scan of the v2 spine finds no path or query parameter that shares a name with a request-body property, so the overwrite cannot happen today.
- Edge 5, low: a stale E6 key on a task-scoped Command would change the computed exemption codes, and the decision-record comparison would fail loudly.
- Edge 6, false: there are no nested decorated types, and a nested public type would fail loudly rather than pass.
- Edge 8, low: the McpCli tests contain no skips, so the all-skipped case is unreachable today, and parsing the summary would add machinery.
- Edge 9, low: guarding against a future `continue-on-error` or regex edit would add assertions for a state that does not exist.

**Rejected parts of surviving findings:**
- Blind 4, extra Idempotency-Key and C10 header cases (low): optional coverage. The typed-exception patch fixes the defect.
- Blind 5, extension value bounds (low): E6 already gives extension value validation to Story 5.13. The allowlist and `--` parts are the decision above.
- Blind 9, no-`TODO` test and IDE0005 enforcement (low): new machinery. Apart from the patched `SearchFolderIndexedFiles.Limit`, the reported cleanup is complete.
- Blind 11, running under `!cancelled()` (low): running after an earlier failure is the intended independent reporting from decision 3. A failed build already makes the job red.
- Edge 4, nested-only discriminator walk (low): no decorated operation has a nested-only discriminator. The `DecisionExemptionCodes` patch resolves C10 only.
- Acceptance 3, message shape (low): header failures already name the operation, field and failing check (for example "C09 task-header required flag").
- Acceptance 6, extra edits (false): `checkout@v7`, `setup-dotnet@v6` and `hexalith.folders.v2.yaml` match the actual workflow and the v2-only rule, so they correct stale text.
- Acceptance 7, the empty Spec Change Log and the spec `status` versus sprint-row mismatch: rejected, because the fix would edit the spec under review. This review syncs the sprint row.

## Design Notes

McpCli's runtime catalog gates need McpCli-owned vectors (`verify_artifact.py` requires `vectors`), so they run at enrollment (McpCli Story 6.1). Since McpCli `fdfe6a3`, enrollment covers Folders by Operation Shape, with at least one live Command and one live Query vector. Folders owns the per-operation routing-string and wire-type tests in its own repository, which is this story's wire-pin and catalog-conformance tests. The rules above yield 3 shapes:
- Commands: String identifiers, Explicit wire type, public tenant, aggregate property, required idempotency key.
- Queries routed by an aggregate property, with a projection type.
- Queries with no aggregate source.

Do not add more shapes without a reason, but do not pin a shape count in a test. After enrollment, a Contracts version bump that changes no vector or inventory is only reported by McpCli and needs no re-approval.

Golden command shape:
```csharp
[HexalithCommand("Acquire the task-scoped workspace lock.", Domain = "folders",
    WireType = "Hexalith.Folders.Commands.LockWorkspace",
    AggregateIdProperty = nameof(FolderId), IdempotencyKeyProperty = nameof(IdempotencyKey))]
public sealed record LockWorkspace(
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("…"), StringLength(128, MinimumLength = 16)] string FolderId,
    …, [property: JsonPropertyName("idempotencyKey"), Description("…")] string IdempotencyKey);
```

## Verification

**Commands:**
- `dotnet build src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj --configuration Release -warnaserror` -- expected: 0 errors and no MCPCLI001.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release` then run `tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests` with `-namespace Hexalith.Folders.Contracts.Tests.McpCli` -- expected: all pass.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release` -- expected: only the known pre-existing failures, namely the gate-script exit-134 group and `Pd10ConformanceSetTests` (stale rows plus the unregenerated new paths, D4). Name each failing test in the Implementation Notes.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes` -- expected: clean (LF line endings).

## Implementation Notes

Implemented 13 Commands, 28 Queries, seven nested request records and 17 enums. The 48-row owner decision record includes exclusions, accepted losses, routing exemptions and downstream gaps. No source outside Contracts, reference submodule, spine or PD10 manifest was changed. All 73 new C# paths are present in the sorted, unique PD10 story-owned list.

Primitive branch-pattern arrays preserve JSON string arrays. Because standard DataAnnotations have no collection-element target, the drift gate compares their item schemas to the identical annotated `DefaultRef` primitive. Conditional `FileMutationRequest/allOf` rules are pinned as a complete schema tree; runtime admission remains Story 5.13 work.

Verification (2026-11-10):

- `dotnet build src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj --configuration Release -warnaserror`: passed, zero warnings/errors and no MCPCLI001.
- `dotnet build Hexalith.Folders.slnx --configuration Release -warnaserror`: passed before and after review, zero warnings/errors. Final log: `/tmp/folders-5-12-final-solution-build.log`.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: passed, zero warnings/errors.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests -namespace Hexalith.Folders.Contracts.Tests.McpCli`: 30/30 passed, no skipped or unrun tests. This includes a real Release pack that rebuilds Contracts and verifies the nuspec and restored/assembly/framework closure.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release`: exit 2, 661/666 passed, exactly the same five PD10 failures as the pre-change baseline (631/636 passed). No gate-script exit-134 failures occurred. Logs: `/tmp/folders-5-12-final-all-tests.log` and `/tmp/folders-5-12-baseline-tests.log`.
- Built Client.Tests and invoked its Release executable with `-class Hexalith.Folders.Client.Tests.ClientGenerationTests`: 75/75 passed, including isolated Contracts restore/generation.
- Dedicated `ContractSpineCiGateTests`: 1/1 passed; the McpCli namespace and root inventory dependency are wired into the spine lane. PowerShell parsing passed.
- Historical real McpCli Core catalog probe: 41 operations, zero diagnostics. Its fixed-effect serialization result predates the later owner decision to remove the ACL payload member; see the review follow-up below.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: passed.
- `git diff --check`: passed. PD10 story-owned path list is sorted and unique; manifest intentionally not regenerated (D4).

Pre-existing, approved PD10 failures:

1. `Pd10V2ContractTests.ConformanceSetBindsEveryCandidateArtifactWithoutChangingReleaseAuthority`
2. `Pd10ConformanceSetTests.GeneratorRejectsOutputThatAliasesARequiredCandidateInput`
3. `Pd10ConformanceSetTests.InventoryCoversEveryRuntimeAndTestCompileInputIncludingExtractedHelpers`
4. `Pd10ConformanceSetTests.ManifestBindsEveryListedCandidateArtifactAndOrderedSetDigest`
5. `Pd10ConformanceSetTests.GeneratorReproducesTheCommittedManifestByteForByte`

The baseline inventory omits `tests/Hexalith.Folders.Server.Tests/OwnerSourceCompositionAssertionIssuer.cs`; the committed manifest also has stale rows. New story-owned paths await the authorized 12.2/1.17 reseal.

Matrix audit: wire-pin row is covered by `EveryCommandWireMatchesTheDomainCatalogAndImmutableInventory`; spine-drift row by `EveryDeclaredFieldRequiredFlagBoundAndEnumMatchesTheV2Spine` and the added/dropped/renamed/required/bound/type/enum mutation self-test; identifier row by all 13 round-trip theory cases, the independent distinct-identifier fixture covering 26 member names, and `AggregateGrammarGapsAreNamedAndRemainUnconverted`; catalog row by `EveryOperationPinsItsRoutingStringsAndHasAClosedSerializableShape` and the exact eligible/decorated-set test. Every covering test ran and passed. Post-review checks also cover canonical serialized bindings, optional omission versus wire-null permission, inline allOf intersections, unsupported composition, encoding/decoded limits, handoff freshness, and references read from the actual packed DLL.

### Review follow-up implementation (2026-11-10)

All 16 approved review patches are implemented. The owner-selected ACL approach
removes `Effect` from both declarations and rejects caller-supplied `effect`
through canonical unknown-member handling; Story 5.13 supplies intent during
translation. This deliberately reverses the earlier Blind-1 serialization patch
recorded in the historical Review Triage Log.

Command header schemas and required flags now come from the spine. Optional task
association on the eight non-task Commands is explicitly carried by
`extensions["taskId"]` under E6 and Story 5.13/G3. The gate rejects mismatched
CommitWorkspace body/header task schemas. Nine mutation cases exercise required
flags, bounds and duplicate body input drift. The exact decision-record column
comparison pins eligibility, CLR/wire/spine/aggregate/exemption/freshness values and
the SHA-256 of the spine; discovery examines every visibility and requires public
decorated types. Both canonical serializer methods and actual null omission are
checked. Three more identifier cases cover legacy trailing punctuation.

The handoff now records all thirteen command-payload translations, reserved actor
routing for C05/Q13/Q35, audit-key uniqueness, bounded range validation, eight exact
V02 paging lint approvals and the seven diagnostics' required folder scope. The
McpCli selector is an independent CI step, guarded by `!cancelled()` and a zero-test
check. The changed SHA-bound workflow, gate script and CI-wiring test explicitly
remain in the 12.2/1.17 reseal scope. The source descriptions are cleaned and all
29 reported unused imports are removed. No new source/test path was introduced;
the existing PD10 story-owned list is sorted, unique and complete.

Current verification:

- Baseline `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: passed, zero warnings/errors; `/tmp/folders-5-12-patch-baseline-build.log`.
- Baseline `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 661/666 passed, the five PD10 failures named above; `/tmp/folders-5-12-patch-baseline-tests.log`.
- Patched `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: passed, zero warnings/errors; `/tmp/folders-5-12-patch-build.log`.
- `dotnet build Hexalith.Folders.slnx --configuration Release -warnaserror`: passed, zero warnings/errors and no MCPCLI001; `/tmp/folders-5-12-patch-solution-build.log`.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli`: 42/42 passed, including the actual Release package-closure gate; `/tmp/folders-5-12-patch-mcpcli-tests.log`.
- The same executable with `-class Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests`: 6/6 passed; `/tmp/folders-5-12-patch-ci-tests.log`.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 673/678 passed, exactly the five approved PD10 failing test names above and no gate-script exit-134 failures; `/tmp/folders-5-12-patch-all-tests.log`. Their byte-mismatch evidence differs as the SHA-bound rows change; the manifest was intentionally not regenerated.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: passed; `/tmp/folders-5-12-patch-format.log`. PowerShell parsing of `tests/tools/run-contract-spine-gates.ps1` and `git diff --check` also passed.
- `dotnet tests/Hexalith.Folders.Client.Tests/bin/Release/net10.0/Hexalith.Folders.Client.Tests.dll -noLogo -noColor -class Hexalith.Folders.Client.Tests.ClientGenerationTests`: 75/75 passed, including isolated Contracts restore/generation; `/tmp/folders-5-12-patch-client-tests.log`.
- AppHost prerequisite limitation: the start/state check was performed after these edits, so it is not pre-edit baseline evidence. No editor lifecycle tools were available. `aspire ps --format Json --non-interactive` returned `[]`. `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` exited 2 because the Debug/source graph imports both `Hexalith.Commons.UniqueIds` 2.30.10.0 (package) and 1.0.0.0 (source): MSB3243 and CS1704 in `Hexalith.Folders.Server`. No AppHost started; the subsequent `aspire ps` was also `[]`. Log: `/tmp/folders-5-12-patch-apphost-start.log`. This runtime prerequisite remains blocked independently of the successful Release solution and focused contract checks; no dependency or topology change was made.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: fixed ACL effects disappear from canonical metadata | medium | Confirmed `McpCliJson.CreateResolver` removes these getter-only properties, and the scratch catalog probe omits `effect`. Patch the existing records without adding public surface and cover the canonical resolver behavior. Raw Gateway admission remains 5.13, including enforcing the operation's fixed effect on directly forwarded JSON. |
| Blind 2: optionality masks spine nullability changes | medium | The comparator treats every optional field as CLR-nullable, so toggling its spine nullable flag is invisible. Patch the gate to distinguish source nullability from optional omission and explicitly record the existing optional-null representation/admission boundary. |
| Blind 3: branch-pattern item constraints borrowed from DefaultRef | false | The identical lexical constraints are intentionally compared for every item schema against the shared annotated primitive; item bound/pattern changes do fail. The decision record expressly states this representation and that runtime McpCli ignores DataAnnotations, so the claimed runtime enforcement is not made. |
| Blind 4: inline allOf constraints skipped | medium | `Resolve` only visits reference intersections and overwrites object members; an inline required/property constraint can evade comparison. Patch to merge supported intersections or fail explicitly on unsupported composition, with a mutation regression. |
| Blind 5: ignored properties still pass reflected-field gate | medium | Reflection retains a property after JsonIgnore removes its effective JSON binding; only aggregate binding is presently checked. Patch to require effective serialized membership for every declared field. |
| Blind 6: inline encoding and decoded limit not pinned | medium | `contentEncoding` and `x-hexalith-decoded-byte-maximum` exist on PutFileInline.contentBytes but are not compared or pinned by the allOf snapshot. Patch their drift coverage and retain admission ownership of decoded-content validation. |
| Blind 7: equal identifier values hide exchanged JSON names | medium | Confirmed equal samples and equal identifier schemas cannot distinguish FolderId/WorkspaceId annotation swaps. Patch independent JSON fixtures with distinct member identifiers and expected wire names. |
| Blind 8: decision-record freshness column not checked | low | The current values match the inventory, but the handoff can diverge silently because freshness is intentionally absent from CLR input. Patch a direct inventory-to-decision-column assertion. |
| Blind 9: dedicated spine lane omits new namespace | medium | The script selects OpenApi only, and its explicit checkout list lacks McpCli although the new tests read its immutable inventory. Patch the selector and the dedicated workflow's root-submodule list. |
| Blind 10: package test inspects loaded assembly rather than packed DLL | low | A Debug runner loads Debug while the subprocess packs Release. Patch the assembly-reference check to inspect the DLL inside the actual nupkg; the nuspec already proves the package's sole declared dependency. |
| Edge-case 1: canonical serializer omits fixed ACL effects | medium | Independently confirmed the same getter-only metadata removal as Blind 1. Group with the ACL serialization patch. |
| Verification gap 1: identifier-to-wire mapping not independently pinned | medium | Pre-verified distinct-identity fixture gap; the existing checks are self-consistent under a folder/workspace annotation exchange. Group with Blind 7's fixture patch. |
| Verification other 1: fixed ACL effects omitted and raw JSON forwarded | medium | Canonical omission is the same defect as Blind 1. Patch record serialization and its test; explicitly name downstream 5.13 enforcement because OperationExecutor forwards JSON without invoking record initializers. |

Review resolution: all nine grouped patch entries were fixed and focused checks passed; one finding was rejected with the evidence above. No new deferred-work entries. Raw-JSON fixed-effect/null/conditional validation remains explicitly assigned to the existing Stories 5.13/5.14 handoff.

Owner-decision follow-up: the later three resolved decisions and sixteen patches
supersede the historical fixed-effect serialization resolution above. In
particular, the previous Blind-1/Edge-case-1/Verification-other-1 remedy is
reversed: there is now no ACL `effect` payload member. Canonical unknown-member
rejection and the explicit Story 5.13 translation handoff replace the private
initializer. The remaining approved review patches are checked above and their
current verification is recorded in Implementation Notes.


### Build follow-up review (2026-11-10)

Three fresh review layers completed; no layer was skipped or failed. Each finding
was assessed before grouping. Blind-1 and Edge-2 share the C10 root-composition
cause and route to one patch. The runtime catalog gap retains its existing
McpCli enrollment owner. The spine digest gate independently rejects changes to
the bound spine bytes; comparator-only mutation probes are not full-suite passes.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind-1: C10 root allOf bypasses Resolve | medium, patch | ExpectedFields passes MutateFilesRequest directly to ObjectFields, which reads only direct properties/required. A root allOf field or tightened changes bound is therefore lost by this comparator. Reuse Resolve before extracting C10 fields and add a mutation regression. |
| Blind-2: array element nullability | low, reject | CompareValue does not inspect NullabilityInfo.ElementType. All current array item contracts are non-null, and the exact spine digest rejects the demonstrated source mutation. Adding element-nullability branches for a hypothetical future shape exceeds a direct correction. |
| Blind-3: effective numeric serialization | low, reject | The probe injects WriteAsString through test-only metadata; current declarations have no numeric converter or NumberHandling override. Supporting arbitrary converter behavior adds machinery; actual exported-schema validation remains the documented enrollment gap. |
| Blind-4: ShouldSerialize suppression | low, reject | The injected callback can hide a required value, but no declaration or canonical resolver installs such a callback. Guarding arbitrary test-only suppression adds logic for a state absent from the current contract surface. |
| Blind-5: additionalProperties comparison | low, reject | ObjectFields omits root closure metadata, but canonical payload handling is pinned to Disallow and McpCli SchemaDeriver.Transform always closes object schemas. The proposed source mutation fails the independent spine digest; more closure assertions would add guards for a hypothetical changed spine. |
| Blind-6: audit key uniqueItems pin | low, reject | The handoff explicitly names uniqueness as Story 5.13 admission work. Static declaration bounds do not implement uniqueness, and the bound spine digest detects a source edit. Adding another deferred-rule snapshot is unnecessary for the current declared field contract. |
| Blind-7: exempted schema discriminator value | low, reject | E1 deliberately removes requestSchemaVersion from the payload and assigns required v2 supply to admission. The exact spine digest rejects v3 source bytes. Additional discriminator guards would extend the explicitly exempted comparison rather than correct a current declaration. |
| Blind-8: ChangeFile/RemoveFile header compatibility | false, reject | C10 deliberately maps to MutateFilesRequest and AddFile scope under E3; ChangeFile and RemoveFile are not separate decorated inventory declarations or C10 routing inputs. The frozen mapping is explicit, and there is no current cross-operation header mismatch. |
| Blind-9: live fixed freshness comparison | low, reject | Current handoff values equal the immutable inventory; freshness is intentionally excluded from payload inputs under V01. A spine freshness mutation fails the pinned digest. New semantic freshness checks would add guards beyond the declared input comparison for hypothetical later drift. |
| Blind-10: root requestBody.required | low, reject | The comparator checks member required flags, not body-envelope optionality. All current command bodies remain required, and changing the spine bytes fails its digest. A new body requirement guard adds logic without a current incompatible declaration. |
| Edge-1: enum vocabulary removal | low, reject | Every current CLR enum has a spine vocabulary. Removal is caught by the exact spine digest even though CompareValue checks enum values only when a vocabulary exists. This repeats the earlier rejected Edge-2 future-drift concern; adding a new enum-presence guard is unnecessary here. |
| Edge-2: C10 root composition | medium, patch | Independently confirms Blind-1: C10 alone bypasses Resolve before ObjectFields. Grouped with Blind-1 for the same resolver correction and regression test. |
| Verification-gap: actual runtime catalog acceptance | medium, defer (existing owner) | Pre-verified: reflection/STJ gates do not run CatalogBuilder, SchemaDeriver or DescriptionLinter; an invalid Example could drop an operation at enrollment. The existing deferred-work entry and Design Notes assign this check to McpCli enrollment. Preserve that boundary and record the new negative-example evidence in the ledger. |


Final review resolution: Blind-1/Edge-2 were patched by resolving the C10 root
through the existing shared resolver. Two mutation cases now prove that a new
required allOf field and a tightened changes.maxItems bound produce field-level
differences. No new public surface or runtime behavior was added.

Final verification after that correction:

- `dotnet build src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj --configuration Release -warnaserror`: exit 0, zero warnings/errors; `/tmp/folders-5-12-review-contracts-build.log`.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: exit 0, zero warnings/errors; `/tmp/folders-5-12-review-tests-build.log`.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli`: exit 0, 44 passed, zero failed/skipped/not-run; `/tmp/folders-5-12-review-mcpcli.log`. All four frozen matrix rows remain covered by the executed wire-mapping, field/bound mutation, identifier round-trip/gap and catalog-shape tests.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 675/680 passed, zero skipped; `/tmp/folders-5-12-review-contracts-all.log`. A direct comparison confirms exactly the same five approved PD10 failure names as the baseline. The independent McpCli lane remains green.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: exit 0; `/tmp/folders-5-12-review-format.log`.

The earlier successful Release solution build, 6/6 CI-wiring checks and 75/75
client-generation checks remain applicable; the review correction touches only
the drift test class. The AppHost source/package collision and the approved PD10
reseal deferral are unchanged. All review findings have a recorded verdict.


### Follow-up review implementation (2026-11-11)

All ten approved follow-up patches are implemented. E6 retains the existing
extension carrier and exemption keys. The handoff now records the domain's
required task extension on all thirteen Commands, the caller-profile allowlist
prerequisite owned by McpCli enrollment and Folders parity setup, and the
platform-wide rejection of extension values ending in `--`. Story 5.13 owns
canonical task-value validation and reconciliation with the five task-scoped
payloads. Domain translation supplies the discriminator accepted by each domain
payload (currently `v1`, matching the existing v2 compatibility seam). E2 now
names McpCli's closed exported schema as its rejection boundary and describes
the local STJ test as mirror evidence. The translation table has its own
subsection, and the indexed-search limit description states 1 through 500 and
the omission default without internal C4 terminology.

The header-drift mutation test now requires `ShouldAssertException` and the
specific affected field in the message for all nine cases. C10 exemption
extraction resolves the request component before inspecting its properties. A
new regression moves `requestSchemaVersion` into a root `allOf` member and
proves both E1 retention and field-level conformance. The independent McpCli CI
step has a 15-minute timeout, the CI guide's validation-mode sentence again
refers to the script, and the exact 2026-11-04 Story 12.3 history entry is
restored from `a9c77c85`. No new path was introduced; the sorted, unique PD10
story-owned list and the conformance manifest remain unchanged.

Verification:

- Pre-edit `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: exit 0, zero warnings/errors; `/tmp/folders-5-12-followup-baseline-build.log`.
- Pre-edit `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 675/680 passed, exactly the five approved PD10 failures already named above; `/tmp/folders-5-12-followup-baseline-tests.log`.
- Patched `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: exit 0, zero warnings/errors; `/tmp/folders-5-12-followup-build.log`.
- `dotnet build Hexalith.Folders.slnx --configuration Release -warnaserror`: exit 0, zero warnings/errors and no MCPCLI001; `/tmp/folders-5-12-followup-solution-build.log`.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli`: exit 0, 45/45 passed, zero skipped/not-run, including the real Release package-closure gate; `/tmp/folders-5-12-followup-mcpcli-tests.log`.
- The same executable with `-class Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests`: exit 0, 6/6 passed; `/tmp/folders-5-12-followup-ci-tests.log`.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 676/681 passed, zero skipped. Failure-name comparison with the pre-edit log proves exactly the same five approved PD10 failures and no new failure; `/tmp/folders-5-12-followup-all-tests.log`. The changed workflow and Contracts/test bytes remain in the existing 12.2/1.17 reseal scope under D4.
- `dotnet tests/Hexalith.Folders.Client.Tests/bin/Release/net10.0/Hexalith.Folders.Client.Tests.dll -noLogo -noColor -class Hexalith.Folders.Client.Tests.ClientGenerationTests`: exit 0, 75/75 passed, including isolated Contracts restore/generation; `/tmp/folders-5-12-followup-client-tests.log`.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: exit 0; `/tmp/folders-5-12-followup-format.log`. `git diff --check` passes. Direct checks confirm the timeout, documentation sentence order, exact restored sprint history and unchanged spine/oracle/path-list bytes.
- Pre-edit Aspire prerequisite: no editor lifecycle tools were available; `aspire ps --format Json --non-interactive` returned `[]`. `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` exited 2 with the existing MSB3243/CS1704 source/package `Hexalith.Commons.UniqueIds` collision in `Hexalith.Folders.Server`; `/tmp/folders-5-12-followup-apphost-start.log`. The subsequent `aspire ps` also returned `[]`, so no AppHost started. No dependency or topology change was made.

The four frozen matrix rows remain covered by the passing wire-name, drift,
identifier and catalog tests. Runtime catalog/live enrollment, downstream
5.13/5.14 admission/handler work, and the previously recorded deferrals retain
their existing owners and are not claimed complete by these declaration fixes.


### Follow-up build review (2026-11-11)

All three fresh layers completed. Edge Case Hunter returned no findings;
Verification Gap Reviewer found no verification gaps. Blind Hunter returned ten
findings. Every finding is triaged individually below before grouping; the four
surviving entries have separate causes and route to small patches.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Follow-up Blind 1: empty inline content rejected by Required | low, patch | The spine requires contentBytes presence but has no minLength; byteLength admits zero. PutFileInline.ContentBytes uses Required with its default AllowEmptyStrings=false, so DataAnnotations consumers reject a legal empty value. Allow empty strings while keeping JsonRequired and add a boundary regression. McpCli itself ignores DataAnnotations. |
| Follow-up Blind 2: enum numeric values under ordinary serializer options | low, reject | The current type-level converters permit integers with ordinary STJ options, while the canonical McpCli options install allowIntegerValues:false and are source-pinned. These declarations' intended consumer uses the latter; no ordinary-options execution consumer of the new records was identified. This repeats the earlier enum-policy/authoring concern; custom converters or wider policy machinery are not needed for current McpCli behavior. |
| Follow-up Blind 3: profile setup replaces existing allowlist | medium, patch | ProfileStore.Set assigns AllowedExtensions=value.Split(',') rather than appending. A caller following the single-key instruction on an existing profile loses other approved keys. Qualify the empty-profile command and explain preserving all existing permitted keys in the comma-separated value. |
| Follow-up Blind 4: Q16 still requires envelope aggregate identity | medium, patch | OperationExecutor resolves call.AggregateId/accessor/constant and rejects an empty result before querying; Q16 deliberately has neither accessor nor constant. The table's none correctly describes its declaration but omits required caller routing. Document the envelope input and Story 5.14 ownership of selecting/validating tenant-wide scope, without inventing an ID or changing the frozen declaration. |
| Follow-up Blind 5: required task extension absent from operation descriptions | medium, patch | All thirteen Commands require canonical extensions.taskId at the domain boundary, while command metadata does not mention it and eight declarations intentionally have no payload TaskId. Add this prerequisite to discoverable Command descriptions, including matching both carriers on the five task-scoped Commands; no payload member or routing change is needed. |
| Follow-up Blind 6: branch/ref descriptions could explain vocabulary | low, reject | The generic descriptions are present and do not claim raw branch/glob input is accepted. Canonical validation and server admission remain separate from the annotations. This repeats the prior rejected boilerplate/Example authoring request; no new contradictory description or current execution regression was demonstrated. |
| Follow-up Blind 7: generic paging/filter descriptions | low, reject | The fields and static bounds match the spine, and V02 deliberately keeps typed paging with runtime semantics owned by 5.14. The earlier review rejected rewriting the existing generic descriptions as authoring. No new incompatible value or incorrect description was shown; a wider paging/filter guide is unnecessary for these corrections. |
| Follow-up Blind 8: serialization samples are not valid complete requests | false, reject | Sample deliberately exercises exact identifier serialization, including legacy/gap values, and never claims whole-request admission. Distinct identifier fixtures independently pin member-to-wire mapping. Invalid unrelated fields do not invalidate those assertions; admission-valid positive request vectors are separately owned by enrollment/5.13/5.14. |
| Follow-up Blind 9: cancellation waits for the pack timeout | low, reject | The wait uses a two-minute timeout independently of test cancellation, so cancellation can delay child termination. This is bounded test infrastructure behavior, with no failing ordinary run; linked cancellation handling adds lifecycle logic for an exceptional path and repeats the prior low pack/timeout concern. |
| Follow-up Blind 10: kill and cleanup race on pack timeout | low, reject | Kill does not await exit, so a timeout could race recursive temporary-directory cleanup. The passing pack runs do not reach this exceptional path. Await/diagnostic/cleanup handling would add lifecycle logic for a low likelihood infrastructure failure, rather than correct current package or operation behavior; retain the existing bounded timeout. |


Final review resolution: all four surviving patches are complete. Empty inline
content validates while missing JSON presence and null DataAnnotations values
remain rejected. Existing profile allowlists are explicitly preserved when taskId
is added. Q16's caller-supplied envelope aggregate routing and Story 5.14 ownership
are named, and all thirteen Command descriptions expose the task extension/profile
prerequisite (including payload equality on the five task-scoped Commands). No new
work was deferred; all earlier deferrals retain their recorded owners.

Final orchestrator verification after review patches:

- `dotnet build src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj --configuration Release -warnaserror`: exit 0, zero warnings/errors; `/tmp/folders-5-12-final-review-contracts-build.log`.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release`: exit 0, zero warnings/errors; `/tmp/folders-5-12-final-review-tests-build.log`.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli`: exit 0, 46/46 passed, zero errors/failed/skipped/not-run; `/tmp/folders-5-12-final-review-mcpcli.log`.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 677/682 passed, zero skipped; `/tmp/folders-5-12-final-review-contracts-all.log`. Exact failure-name comparison confirms the same five approved PD10 failures as the pre-edit baseline, with no new failing test. D4 resealing remains with 12.2/1.17.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: exit 0; `/tmp/folders-5-12-final-review-format.log`.
- `dotnet build Hexalith.Folders.slnx --configuration Release -warnaserror`: exit 0, zero warnings/errors and no MCPCLI001; `/tmp/folders-5-12-final-review-solution-build.log`.

All four frozen matrix rows are covered by executed, passing tests: wire pin by
EveryCommandWireMatchesTheDomainCatalogAndImmutableInventory; spine drift by
EveryDeclaredFieldRequiredFlagBoundAndEnumMatchesTheV2Spine plus the field/header/
allOf mutation regressions; identifier round-trip by all sixteen theory cases,
IndependentJsonFixturesPinDistinctIdentifiersToEveryClrMemberAndRoutingKey and
AggregateGrammarGapsAreNamedAndRemainUnconverted; catalog shape by
EveryOperationPinsItsRoutingStringsAndHasAClosedSerializableShape and the exact
eligible/decorated-set test. The new empty-inline regression and real Release
package-closure test also ran and passed. Earlier 6/6 CI-wiring and 75/75 client
checks remain recorded above; the final complete Contracts run includes the CI
checks. The pre-edit Aspire MSB3243/CS1704 blocker remains independently recorded.
Frozen intent, reference submodules, spine/oracle, manifest and PD10 path-list
bytes remain unchanged during this follow-up. The spec is done and its sprint row
is handed off as review under the bmad-build status-sync rule.
