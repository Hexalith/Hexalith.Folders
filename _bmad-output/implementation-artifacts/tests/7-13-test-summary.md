# Test Automation Summary

> Canonical latest-run summary for Story 7.13. Durable per-story copy: [`7-13-test-summary.md`](./7-13-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-13-publish-api-sdk-cli-and-mcp-consumer-references.md`
**Feature under test:** Consumer API/SDK, CLI, MCP, auth, lifecycle-diagram, example-validation, and release-readiness documentation gates.

## Generated Tests

### API Tests

- [x] Not applicable as live endpoint tests; Story 7.13 publishes static consumer references and gate wiring, not runtime API behavior.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` - Expanded static end-to-end conformance from 11 to 22 facts. New QA guardrail/review facts cover OpenAPI security/header/error-shape details, SDK golden lifecycle and idempotency-helper trap, CLI command/credential/mutation rules, MCP per-group counts and transport/discovery rules, authentication S-2/claim provenance, and Mermaid lifecycle diagram body/edge checks against C6 and the spine.
- [x] `tests/tools/run-consumer-docs-gates.ps1` - End-to-end consumer-docs release-readiness gate that runs the conformance suite, emits `_bmad-output/gates/consumer-docs/latest.json`, and fails closed on missing, malformed, vacuous, unsafe, or recursive-submodule evidence.

## Coverage

- Published doc surfaces: 8/8 existence and metadata-only coverage (`api-reference`, `quickstart`, `cli-reference`, `mcp-reference`, `authentication`, and 3 lifecycle diagrams).
- API reference: spine-derived operation/tag inventory, OIDC scheme, mutating header triple, GET no-idempotency rule, `ValidateProviderReadiness` POST-as-query exception, and Problem Details required fields.
- SDK reference: 9-step golden lifecycle ordering, compile-checked lifecycle example, `HelperSchemaVersion`, and `(folderId, workspaceId, taskId)` idempotency helper trap.
- CLI reference: 7/7 groups, 40 leaf commands, credential precedence, mutation/query option split, `commit create`, and exit-code table.
- MCP reference: 47/47 tools, 2/2 resources, 8/8 tool-group counts, stdio/stderr transport, assembly discovery, no manifest, task/idempotency rules, 45 failure kinds, and `range_unsatisfiable -> internal_error` drift note.
- Auth and diagrams: S-2 token validation, claim provenance, credential sources, C6 disposition/event vocabulary, exact architecture C6 transition edges, canonical file-commit operations, and fixed auth/ACL deny-by-default order.

## Validation

- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1` passed with 0 warnings and 0 errors.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests --logger console;verbosity=minimal` passed: 22 total, 0 failed.
- `pwsh ./tests/tools/run-consumer-docs-gates.ps1 -SkipRestoreBuild` passed and regenerated `_bmad-output/gates/consumer-docs/latest.json`.

## Checklist Validation

- API tests generated if applicable: not applicable for live endpoints; static API reference conformance is source-backed against the OpenAPI spine.
- E2E tests generated if UI exists: browser UI is not applicable; workflow/gate coverage exercises the implemented consumer documentation release behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, `System.Text.Json`, `GeneratedRegex`, and the existing PowerShell gate-script pattern.
- Happy path: passed; every required published surface and CI/gate hook is present and source-aligned.
- Critical error cases: passed; negative controls cover missing docs, stale inventories, stale exit-code rows, leaked paths/tokens/real issuers, malformed manifest entries, malformed JSON, and recursive-submodule setup.
- Test quality: passed; tests have clear descriptions, no sleeps, no order dependency, and derive inventories from source artifacts rather than hard-coded doc counts where source authority exists.
- Output: passed; summary created at the workflow default path and durable Story 7.13 path.
