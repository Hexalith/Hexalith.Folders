## Git Submodules

- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Initialize only submodules at the root of the repository.
- For repositories with submodules, initialize/update only root-level submodules by default.
- Avoid `git submodule update --init --recursive` and similar recursive submodule commands unless nested submodule initialization is explicitly requested.

Initialize only root-level submodules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.EventStore Hexalith.FrontComposer Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so keep default setup limited to the root-level module inventory.
