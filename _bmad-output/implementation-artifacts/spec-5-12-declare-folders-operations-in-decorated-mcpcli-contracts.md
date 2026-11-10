---
title: '5.12 Declare Folders operations in decorated McpCli Contracts'
type: 'feature'
created: '2026-11-10'
status: 'draft'
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

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Wire pin | each decorated Command | `WireType` equals the `FoldersCommandCatalog` entry `Hexalith.Folders.Commands.<Name>` | mismatch or missing entry fails the test |
| Spine drift | a field added, renamed or dropped, or a required flag or bound changed, on either side | the drift gate names the type, field and difference | fails unless the field is in the exemption table |
| ID round-trip | sample folder, workspace, task and provider-binding IDs in the v2 opaque grammar and the legacy grammar | STJ serialize/deserialize is byte-identical, case preserved | IDs McpCli rejects as aggregate IDs (an ID ending in `_`/`-`, or a legacy ID with a leading `.`/`_`/`-`) are asserted as the named gap list |
| Catalog shape | all decorated types | operation names unique after `KebabCase.FromTypeName`; no forbidden member types (object/JsonElement/polymorphic) | violation fails the test |

</frozen-after-approval>

## Open Questions

1. **Execution gate.** The manifest lists 5.12 as `execution_authorization_status: held`, and its prerequisite Story 1.17 is `backlog`: the parent spec is in-progress and blocked on publication, deployment and owner decisions. Options: **(a) authorize now, scoped.** Record a DEC-EXEC-5.12 authorization against the v2 spine already released in Contracts 1.3.3 (oracle `contract_spine_sha256` `d417afd8…`). If 1.17 later changes the spine, the drift gate reddens. **(b) hold.** Leave this spec `draft` until 1.17 closes.
2. **Agent eligibility.** Under McpCli Story 5.12 (`fdfe6a3`), decorating a type is the owner's consent to expose it to agents without human confirmation. Vectors must cover operation shapes, not each operation, and a contract named by a `defer` row must stay undecorated.
   - **No exclusions by marker.** No Folders candidate is marked UI-only or confirmation-required; `HumanPolicy` is authentication only.
   - **High-impact candidates to consent to explicitly:** C01 archive, C05 provider-binding configuration, C10 (includes file removal) and C13 ACL revoke.
   - **Options:**
     - **(a) All 48** (C01–C13, Q01–Q35). Story 5.14 must then serve the 7 ops-console diagnostics from durable projections, which do not exist; they are seed-backed for MVP.
     - **(b) 41.** Exclude Q04, Q06, Q10, Q12, Q15, Q17 and Q20. Each needs a product-approved C13 not-applicable declaration later.
     - **(c) A custom list.**
3. **Behavior losses for McpCli Story 5.8.** For each one, accept the loss (name the replacement experience) or keep it `defer`:
   - **V04** local-file and stdin input, and the upload helper's derived evidence. Replacement: the caller sends inline content and evidence within 262144 bytes; staging waits on McpCli 5.2.
   - **V07** `--allow-auto-key` and legacy `sha256:` keys. Replacement: the caller supplies an uppercase ULID key.
   - **V09** legacy credential precedence, human output and exit codes 64–77. Replacement: McpCli profiles, JSON output and exit 2.
   - **R01/R02** MCP resources. Replacement: queries Q28 and Q30.
4. **PD10 conformance manifest.** New Contracts files must be listed in `scripts/pd10-v2-story-owned-paths.txt` and the manifest regenerated. Otherwise `Pd10ConformanceSetTests` reddens; it is already red on stale rows, and resealing was deferred to 12.2/1.17. Options: **(a)** add the paths and regenerate here, which reseals the other stale rows too. **(b)** add only the paths list, leave regeneration to the 12.2/1.17 reseal, and report the red test as pre-existing plus expected.

## Code Map

- `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` -- add `<PackageReference Include="Hexalith.McpCli.Abstractions" />`. The version comes from Builds `HexalithMcpCliVersion` 2.0.1 (`references/Hexalith.Builds/Props/Directory.Packages.props:21,107`). The package is NuGet-only (there is no `HexalithMcpCliRoot`), so no ProjectReference (`ScaffoldContractTests:179`).
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml` -- the authoritative spine; read-only. Request schemas are camelCase with `additionalProperties: false`. IDs use `OpaqueIdentifier` (`:18761`: `^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$`).
- `src/Hexalith.Folders/Routing/FoldersCommandCatalog.cs` -- wire names (`:75`) and `task:` flags; read-only. Its snake_case field lists are idempotency descriptors, not the payload shape.
- `src/Hexalith.Folders.Contracts/FolderOwnerReadQuery.cs` -- the existing query-type naming precedent (`Hexalith.Folders.Queries.*`); leave unchanged.
- `references/Hexalith.McpCli/src/Hexalith.McpCli.Abstractions/*.cs` -- the attribute surface. `KebabCase.FromTypeName` is reusable in tests.
- `references/Hexalith.McpCli/src/Hexalith.McpCli.Core/Catalog/CatalogBuilder.cs:93-250`, `Routing/RoutingResolver.cs`, `Schema/SchemaDeriver.cs:226-331` -- the runtime rules the conformance test mirrors: the aggregate member must be a serialized string, the aggregate grammar is 1–256 `[A-Za-z0-9._-]` starting and ending alphanumeric, a query `WireType` may not contain `:`, and `ProjectionType` is kebab and at most 64 characters.
- `tests/Hexalith.Folders.Contracts.Tests/` -- xUnit v3, Shouldly and YamlDotNet; new tests go under `McpCli/`.
- `tests/Hexalith.Folders.Contracts.Tests/**/Pd10ConformanceSetTests*`, `scripts/pd10-v2-story-owned-paths.txt`, `scripts/generate-pd10-v2-conformance-set.py` -- the PD10 manifest coupling (OQ4).
- `tests/Hexalith.Folders.Contracts.Tests/**/ClientGenerationTests.cs:337-372` -- restores a copy of the Contracts csproj; must stay green.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` -- add the versionless Abstractions reference -- the A-12 sole dependency.
- [ ] `src/Hexalith.Folders.Contracts/FoldersModule.cs` -- the assembly `HexalithModule` declaration -- module identity.
- [ ] `src/Hexalith.Folders.Contracts/Commands/*.cs` and `Commands/Models/*.cs` -- one decorated record per eligible C-candidate, plus nested types and enums -- G1 commands.
- [ ] `src/Hexalith.Folders.Contracts/Queries/*.cs` and `Queries/Models/*.cs` -- one decorated record per eligible Q-candidate -- G1 queries.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersWireNameMappingTests.cs` -- pin every Command wire name against `FoldersCommandCatalog` and the inventory table -- AC wire mapping.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/ContractSpineDriftGateTests.cs` -- compare fields, required flags, bounds and enum sets to the v2 spine, with one explicit exemption table -- AC drift gate.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/FoldersIdentifierRoundTripTests.cs` and `McpCliCatalogConformanceTests.cs` -- the ID matrix rows and the catalog-shape rules; assert that the eligible set equals the decorated set exactly -- AC identifiers and AC eligibility.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/PackedContractsClosureTests.cs` -- pack Contracts in Release; the nuspec's only dependency is `Hexalith.McpCli.Abstractions`, with no framework reference other than `Microsoft.NETCore.App` -- AD-15.
- [ ] `docs/contract/mcpcli-folders-operation-declarations.md` -- one row per candidate (eligibility, CLR type, wire name, spine operation, aggregate, exemptions), the V04/V07/V09/R01/R02 decisions, and the named gaps -- the input to McpCli Story 5.8.
- [ ] `scripts/pd10-v2-story-owned-paths.txt` (plus the manifest, per OQ4) -- list the new paths -- PD10 coverage.

**Acceptance Criteria:**
- Given a Release build with `TreatWarningsAsErrors`, when the solution builds, then MCPCLI001 reports nothing and every existing Contracts.Tests test outside the known pre-existing failures stays green.
- Given the decision record, when it is compared with the decorated assembly, then the eligible set and the decorated set are equal, and each behavior loss names its rationale, replacement and affected rows.

## Design Notes

McpCli's runtime catalog gates need McpCli-owned vectors (`verify_artifact.py` requires `vectors`), so they run at enrollment (McpCli Story 6.1). Since McpCli `fdfe6a3`, enrollment covers Folders by Operation Shape, with at least one live Command and one live Query vector. Folders owns the per-operation routing-string and wire-type tests in its own repository, which is this story's wire-pin and catalog-conformance tests. The rules above yield 3 shapes:
- Commands: String identifiers, Explicit wire type, public tenant, aggregate property, required idempotency key.
- Queries routed by an aggregate property, with a projection type.
- Queries with no aggregate source.

Do not add more shapes without a reason.

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
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release` -- expected: only the known pre-existing failures (the gate-script exit-134 group, and Pd10 per OQ4).
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes` -- expected: clean (LF line endings).

## Implementation Notes

## Spec Change Log

## Review Triage Log
