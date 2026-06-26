## Shared Hexalith LLM Instructions

Before starting any work in this repository, read and follow
[`references/Hexalith.AI.Tools/hexalith-llm-instructions.md`](./references/Hexalith.AI.Tools/hexalith-llm-instructions.md).

## Git Submodules

- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Initialize only submodules declared by this repository under `references/`.
- For repositories with submodules, initialize/update only the `references/` submodule paths by default.
- Avoid `git submodule update --init --recursive` and similar recursive submodule commands unless nested submodule initialization is explicitly requested.

Initialize only repository-declared submodules under `references/`:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

Do not use:

```text
git submodule update --init --recursive
```

Nested submodules must only be initialized when a user explicitly requests nested submodule work.

Recursive initialization can pull nested dependencies unexpectedly, so keep default setup limited to the `references/` module inventory.
