---
title: 'Resolve five deferred YAML governance issues'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five related YAML governance input items: DW-68 (clone fidelity), DW-247 (culture-invariant serialization), and the three deferred follow-ups from `spec-five-deferred-governance-inputs.md` for operation-ID scalar types, OpenAPI path/method key shapes, and YAML parser inner exceptions. Governance enumeration must reject non-string operation IDs and malformed keys with bounded prerequisite-drift diagnostics, while preserving quoted/explicit strings, valid paths and permitted extensions/metadata. Malformed YAML must not reveal parser detail through complete exception output. Safety serialization must be independent of the ambient culture. Pin existing clone fidelity and isolation with a regression; preserve library behavior already shown correct and explain why comments are unavailable after initial parsing.

Preserve canonical contracts, approval evidence, generated artifacts and dependency pins. Verify positive and negative cases, resolve all five ledger entries with evidence, complete independent review, commit and push changes on main as requested.

</frozen-after-approval>

## Implementation Notes

- Small, reversible footprint: two existing contract-test helper files, focused partial tests, a brief gate documentation update, ledger and this spec. No product questions, migrations or new public API. Git publication is explicitly authorized.
- Baseline: clean main at 8272bc5071c5b7ee8f2a971130372dd160e14771.
- Code map: `GovernanceCompletenessGateTests.cs` owns `LoadOpenApiOperationIds`, `LoadYamlStream`, `CloneRow`; `SafetyInvariantGateTests.cs` owns `SerializeYaml`. Existing InputTests preserve explicit string tags for null-like operation IDs. Restrict new type checks to OpenAPI enumeration rather than changing shared RequiredScalar semantics.
- Use YAML 1.2 core scalar resolution for untagged plain scalars; explicit !!str and non-specific ! preserve strings. Reject explicit non-string/custom tags. Source: https://yaml.org/spec/1.2.2/#1032-tag-resolution . Path keys must be strings beginning with / or non-empty x- extensions; path-item fields must be lowercase HTTP verbs, known metadata or non-empty x- extensions. Keep unsupported $ref behavior fail-closed.
- Investigation reproduced CloneRow preserving explicit tags, anchors, alias sharing and scalar styles under current YamlDotNet; prefer compatibility regression over rewriting correct behavior. Comments are discarded at initial parse.
- Required baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exits 2, Server CS1704 duplicate Hexalith.Commons.UniqueIds source/package references. `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` confirms no running AppHost. The focused Debug/source contract-test build passes with zero warnings/errors.

- The new negative tests reproduced all 34 loader failures before correction. OpenAPI enumeration now validates YAML 1.2 scalar types and keys before skipping metadata/extensions; the YAML loader omits parser inner exceptions. A generated invariant regex covers core non-string scalars without numeric overflow or ambient-culture dependence.
- Deeper DW-68 regressions revealed collection-tag loss that the initial scalar-tag probe missed. Replaced serialize/parse cloning with a reference-identity graph copy preserving tags, anchors, styles, shared aliases, recursive collections and isolation; scalar behavior remains unchanged. Comments and parser source marks are outside this representation-copy guarantee.
- Diagnostic tests assert bounded messages and absence of input markers in complete exception output; runtime stack traces still contain source locations in a normal Debug build. The safety serializer explicitly uses CultureInfo.InvariantCulture with French, Arabic and Turkish compatibility checks.

- Verification passed: Debug/source build with zero warnings/errors; all four selected governance/safety/authorization/provider classes passed 187/187, zero skipped. Focused `dotnet format whitespace --no-restore --verify-no-changes` and `git diff --check` passed. Origin/main was fetched and has no divergence from the starting revision.

- Final post-review verification: Debug/source build passes with zero warnings/errors; all four classes pass 199/199 with zero skipped. Focused whitespace verification and git diff --check pass. Independent review patched three coverage gaps and recorded three inherited validation follow-ups. The Conventional Commit message passes commitlint; fetched origin/main has no divergence.

## Verification

- `DOTNET_PROCESSOR_COUNT=2 dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj -c Debug -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -v:q`: passed, zero warnings/errors.
- `DOTNET_PROCESSOR_COUNT=2 dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests -noLogo -noColor`: final post-review pass 199/199, zero skipped.
- `DOTNET_PROCESSOR_COUNT=2 dotnet format whitespace tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --verify-no-changes --include tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.YamlLoaderTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.YamlCloneTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.YamlTests.cs`: passed.
- `git diff --check`: passed.


## Review Triage Log

- Medium, deferred: string-tagged structural-key lookup errors and tagged $ref bypass reproduce in the baseline. Recorded a separate tag-aware required-key/reference lookup follow-up; narrowed documentation to operation-ID values.
- Medium, deferred: incompatible tags on mapping collections are accepted by inherited node-kind-only checks. Recorded a bounded collection-tag validation follow-up.
- Low, deferred: inherited known path metadata value shapes are skipped rather than schema-validated. Documented enumeration scope explicitly and recorded a dedicated schema-validation follow-up.
- Low, patched: added literal/folded block-style operation-ID and clone regressions.
- Low, patched: added recursive-mapping and scalar-alias clone identity/isolation regressions.
- Low, patched: added core-scalar boundary positives (1_000, 0o8, 0XAB, nUlL) and numeric negatives (.nan, +.INF, arbitrary-precision decimal text).
