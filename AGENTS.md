# AI assistant instructions

Before working in this repository, read
[`hexalith-llm-instructions.md`](./references/Hexalith.AI.Tools/hexalith-llm-instructions.md)
(in the `references/Hexalith.AI.Tools` submodule) and follow it.

## Git Submodules

- Initialize the root-declared submodules with:
  `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.
- Do not run recursive submodule initialization unless it is explicitly scoped so that nested submodules are not initialized.
- If nested submodules are initialized accidentally, deinitialize them before continuing.
