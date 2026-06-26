# Hexalith.Folders

Hexalith Folder Management

## Setup

Restore and build from the repository root:

```text
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx --no-restore
```

Initialize only repository-declared submodules under `references/`:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so default setup is intentionally limited to the `references/` submodule inventory.
