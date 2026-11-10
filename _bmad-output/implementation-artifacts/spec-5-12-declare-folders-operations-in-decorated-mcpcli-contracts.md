---
title: '5.12 Declare Folders operations in decorated McpCli Contracts'
type: 'feature'
created: '2026-11-10'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_revision: 'd873861677982a737d131180bcf79f06caee034d'
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
- [ ] `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` -- add the versionless Abstractions reference -- the A-12 sole dependency.
- [ ] `src/Hexalith.Folders.Contracts/FoldersModule.cs` -- the assembly `HexalithModule` declaration -- module identity.
- [ ] `src/Hexalith.Folders.Contracts/Commands/*.cs` and `Commands/Models/*.cs` -- one decorated record for each of C01–C13, plus nested types and enums -- G1 commands.
- [ ] `src/Hexalith.Folders.Contracts/Queries/*.cs` and `Queries/Models/*.cs` -- one decorated record for each of the 28 eligible Q-candidates (D2); never decorate Q04, Q06, Q10, Q12, Q15, Q17 or Q20 -- G1 queries.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersWireNameMappingTests.cs` -- pin every Command wire name against `FoldersCommandCatalog` and the inventory table -- AC wire mapping.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs` -- compare fields, required flags, bounds and enum sets to the v2 spine, with one explicit exemption table -- AC drift gate.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersIdentifierRoundTripTests.cs` and `McpCliCatalogConformanceTests.cs` -- the ID matrix rows and the catalog-shape rules. Pin each operation's routing strings (`Domain`, `WireType`, `ProjectionType` and aggregate source), and assert that the eligible set equals the decorated set exactly. Folders owns the per-operation routing-string and wire-type tests (McpCli Story 5.12) -- AC identifiers and AC eligibility.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/PackedContractsClosureTests.cs` -- pack Contracts in Release; the nuspec's only dependency is `Hexalith.McpCli.Abstractions`, with no framework reference other than `Microsoft.NETCore.App` -- AD-15.
- [ ] `docs/contract/mcpcli-folders-operation-declarations.md` -- one row per candidate for all 48 (eligibility per D2 with the exclusion reason, CLR type, wire name, spine operation, aggregate, exemptions), the D1 authorization, the D2 consent and live-vector chain, the D3 losses (rationale, replacement, affected rows), and the named gaps -- the input to McpCli Story 5.8.
- [ ] `scripts/pd10-v2-story-owned-paths.txt` -- add every new `src/` and test path, sorted and unique; do not regenerate the manifest (D4) -- PD10 coverage.

**Acceptance Criteria:**
- Given a Release build with `TreatWarningsAsErrors`, when the solution builds, then MCPCLI001 reports nothing and every existing Contracts.Tests test outside the known pre-existing failures stays green.
- Given the decision record, when it is compared with the decorated assembly, then the eligible set and the decorated set are equal, and each behavior loss names its rationale, replacement and affected rows.

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

## Spec Change Log

## Review Triage Log
