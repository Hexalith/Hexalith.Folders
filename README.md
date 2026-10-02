# Hexalith.Folders

Hexalith Folder Management

## Setup

Restore and build from the repository root:

```text
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx --no-restore
```

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
git submodule update --init --checkout references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.McpCli references/Hexalith.Memories references/Hexalith.Platform references/Hexalith.PolymorphicSerializations references/Hexalith.Projects references/Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so default setup is intentionally limited to the `references/` submodule inventory.
