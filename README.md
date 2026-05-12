# Hexalith.Folders

Hexalith Folder Management

## Setup

Restore and build from the repository root:

```text
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx --no-restore
```

Initialize only root-level submodules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so default setup is intentionally limited to the root-level submodule inventory.
