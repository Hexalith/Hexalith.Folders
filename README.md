# Hexalith.Folders

Hexalith Folder Management

## Setup

For local development with the declared source dependencies available, restore and build Debug from
this repository root with both source-mode properties applied globally:

```text
dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet build Hexalith.Folders.slnx -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
```

The qualified individual Server test-project build uses the same supported inputs:

```text
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
```

Plain Debug without these global properties can mix source dependencies with sibling package defaults,
causing CS1704/MSB3243 for Commons.UniqueIds. Do not substitute a no-project-rebuild fallback or a fabricated
source-mapping marker. CI and Release continue to use their centrally selected package dependencies.

Automatic submodule checkout skips the seven cyclic source roots through their
`update = none` settings. The four acyclic roots (AI.Tools, Builds, Commons and
PolymorphicSerializations) remain available to default package-mode setup and
dependency updaters.

Existing `submodule.<name>.update` settings in local `.git/config` take precedence
over `.gitmodules` during initialization, so an older clone may retain its
checkout strategy. The command-line `--checkout` overrides either setting.
Repeat the canonical command below after parent gitlinks change to check out the
newly recorded commits; plain updates continue to skip roots configured as `none`.
Listing every root explicitly also avoids selection through `submodule.active`.

For full source setup, explicitly override that default with `--checkout` and
initialize only repository-declared submodules under `references/`:

Do not initialize nested submodules by default.

```text
git submodule update --init --checkout references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.McpCli references/Hexalith.Memories references/Hexalith.Platform references/Hexalith.Parties references/Hexalith.PolymorphicSerializations references/Hexalith.Projects references/Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so default setup is intentionally limited to the `references/` submodule inventory.

## Legacy compatibility surfaces

These are versioned compatibility binaries. They are not retired and stay outside every Hexalith.Platform composition.

- `Hexalith.Folders.Cli` (.NET tool `folders`): removal gate "Folders inventory gates" ([technical-tools inventory 1.0.2](https://github.com/Hexalith/Hexalith.McpCli/blob/c70ca9784da393cb1929741c6a6c9564717eace5/_bmad-output/implementation-artifacts/technical-tools-migration-inventory-v1.md) PS10; [Folders inventory 1.0.0](https://github.com/Hexalith/Hexalith.McpCli/blob/c70ca9784da393cb1929741c6a6c9564717eace5/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md)).
- `Hexalith.Folders.Mcp` (stdio MCP server): removal gate "Folders inventory gates" ([technical-tools inventory 1.0.2](https://github.com/Hexalith/Hexalith.McpCli/blob/c70ca9784da393cb1929741c6a6c9564717eace5/_bmad-output/implementation-artifacts/technical-tools-migration-inventory-v1.md) PS11; [Folders inventory 1.0.0](https://github.com/Hexalith/Hexalith.McpCli/blob/c70ca9784da393cb1929741c6a6c9564717eace5/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md)).
