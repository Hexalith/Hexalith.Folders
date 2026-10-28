---
title: 'Resolve five deferred SDK helper input issues'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete five related deferred SDK generation and canonicalization entries:

1. DW-106: Reject a relative or empty `--repository-root` before reading inputs or modifying generated artifacts.
2. DW-110: Apply the existing canonical identifier validation to direct `IdempotencyField.ToCanonicalLine()` callers, including control characters, BOM, line separators and malformed UTF-16. Preserve value escaping and valid canonical lines/hashes.
3. DW-113: Reject empty or malformed parameter references and empty/missing parameter names before generated output is written. Preserve canonical local parameter references and inline names.
4. DW-116: Generate null propagation at every intermediate reference in nested idempotency paths. Keep the established root-property presence convention: absent root is omitted; present root with a null intermediate or leaf is present-null.
5. DW-213: Give exact duplicate field names a duplicate diagnostic even when the sequence also violates order. Keep ordinal, case-sensitive field identity and ordering.

Add focused regressions for malformed and valid inputs, nested helper execution and canonical generation stability. Reconcile these five ledger entries, review the diff, run relevant checks, and commit and push to main as requested. Scope is confined to SDK helper input consistency; no contract, dependency or submodule updates.

</frozen-after-approval>

## Implementation Notes

- Investigation: `Generation/Program.cs` reads repository-root without checking it; `EnumerateParameters` accepts empty declarations; `ResolveField` uses one null-conditional hop. `HexalithIdempotencyHasher.EnsureDeclaredOrder` validates field paths while the public struct's direct method skips those checks. Reuse the same validator and split the struct into its own file to follow repository guidance.
- No unresolved intent choices or data migrations. Small local footprint: generator, hasher/field, focused Client.Tests, ledger and this spec. User explicitly authorized the final commit/push.
- Folders main was clean. During investigation another session advanced it from `9141b99b` to `56550414c58db9b2d46a1a8b45fc77a3156cbee0` with a Memories source-mapping fix; the clean tree and this task were preserved.
- Baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2: Server CS1704 from source/package `Hexalith.Commons.UniqueIds` conflict. `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` reports no running AppHost. Focused Debug/source checks are the validation path; keep startup failure separate.
- Implemented the five fixes and split `IdempotencyField` into its own documented source file. The existing test class is partial so focused input regressions reuse its process runner and repository/cleanup conventions. Nested-path verification compiles an actual emitted helper and executes it at every null depth.
- Focused ClientGenerationTests pass 70/70, including isolated byte-for-byte regeneration and malformed-input artifact preservation. The Debug/source Client.Tests build passes with zero warnings/errors.
- Full Client.Tests suite passes 387/387, with zero skipped tests. Whitespace verification passes separately for the generator project and touched solution files. Fetch confirms main equals origin/main before the task commit.
- Independent review identified referenced-name validation and a legal-unused-name regression, both patched. Parameter components are now resolved and validated without changing their established helper aliases. The nested fixture now defines its complete schema and generates real DTOs with the repository's pinned NSwag before compiling/executing the emitted helper. Probe build configuration follows the surrounding suite and assertion diagnostics use existing path redaction.
- Final post-review Debug/source build passes with zero warnings/errors. The full Client suite passes 392/392, zero skipped, including real nested DTO/helper generation and byte-for-byte canonical regeneration. Both focused whitespace checks pass. All five review findings were patched; no follow-up work was deferred.

## Review Triage Log

- Medium, patched: syntactically valid parameter references could target missing definitions or definitions with missing/empty names. Resolve their targets and run the inline name guard; added negative cases require both artifacts to remain unchanged.
- Medium, patched: requiring a nonempty normalized helper identifier rejected legal unused query names such as `_`. Keep nonblank OpenAPI names and validate helper identifiers only through existing field resolution; positive punctuation-name cases preserve generation.
- Medium, patched: the nested regression supplied handwritten DTOs not fully described by its schema. Define all nested schemas and generate actual DTOs using the pinned NSwag tool before compiling/executing the real helper.
- Low, patched: the nested probe defaulted to Debug even in a Release suite. Pass the surrounding test assembly's build configuration to `dotnet run`.
- Low, patched: some assertions embedded raw subprocess diagnostics. Redact captured output with the existing temporary/repository path convention before asserting content.

## Verification

- `DOTNET_PROCESSOR_COUNT=2 dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj -c Debug -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -v:q`: zero warnings/errors.
- `DOTNET_PROCESSOR_COUNT=2 dotnet tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll -class Hexalith.Folders.Client.Tests.ClientGenerationTests`: 70/70 passed, including canonical artifact regeneration.
- `DOTNET_PROCESSOR_COUNT=2 dotnet tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll`: final post-review result 392/392 passed, zero skipped.
- `dotnet format whitespace` with `--no-restore --verify-no-changes --include` for all touched C# paths passes for `Hexalith.Folders.slnx` and the standalone generator project.
- `git diff --check`: passed. The separate pre-change AppHost startup blocker is recorded above.
