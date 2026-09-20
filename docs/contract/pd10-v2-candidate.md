# PD10 v2 Candidate

Status: `candidate-awaiting-a6b`. This is a generated review candidate, not release authority or runtime evidence.

The candidate Contract Spine is `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml`. It contains
exactly 49 operation identities, fourteen access states, and eleven protected operation families. The supported
production host does not map this surface. Product, Architecture, and Security must approve the exact final
conformance-set digest at A6b before later Section 9 and A8 work may authorize exposure.

In-process parity hosts opt into `UsePd10V2CandidateCompatibilitySeam` before routing. That isolated seam
rewrites candidate requests into the historical implementation and normalizes protected denial envelopes for
generated-consumer verification; `Program.cs` and supported deployment profiles never select it.

## Canonical authorization outcomes

Every protected operation declares the same pre-observation authorization boundary:

| Condition | Status | Category | Code | Action | Visibility |
|---|---:|---|---|---|---|
| Authentication missing or invalid | 401 | `authentication_failure` | `authentication_required` | `check_credentials` | `redacted` |
| Fresh negative authority or scope/binding failure | 404 | `tenant_access_denied` | `resource_unavailable` | `no_action` | `redacted` |
| Stale, unavailable, conflicting, or incomplete authority | 503 | `read_model_unavailable` | `projection_unavailable` | `retry` | `redacted` |

Post-authentication 403 and caller-visible `not_found`, `cross_tenant_access_denied`, and
`audit_access_denied` are not part of the protected v2 vocabulary. Folder authorization precedes task,
diagnostic, provider, resource, content, audit, count, filter, and search observation. Task status proves its
task-to-folder binding only after fresh parent authorization.

## Generated consumers and parity

- The checked-in .NET client and idempotency helpers are generated from v2.
- CLI maps `read_model_unavailable` to exit 73 and `concurrency_conflict` to exit 77.
- MCP maps `concurrency_conflict` to the same named failure kind.
- UI keeps denial and authority-unavailable dispositions distinct without rendering raw response text.
- `tests/fixtures/previous-spine.yaml` fingerprints the historical v1 method/path, status, and error surface.
- `tests/fixtures/parity-contract.yaml` is the generated 49-row C13 v2 oracle, including status sets.

## Reproduce the candidate

```text
python3 scripts/generate-pd10-v2-contract.py --source src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml --matrix docs/contract/authorization-matrix.md --output src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml
dotnet msbuild src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersIdempotencyHelpers /p:Configuration=Debug
dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root . --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --output tests/fixtures/parity-contract.yaml
python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .
```

The last command writes `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`.
The manifest binds each candidate path to its raw-byte SHA-256 and binds the ordered path/digest inventory to a
candidate-set SHA-256. It deliberately excludes the historical v1 spine, OQ3 approval record, lifecycle status,
and deployment/release configuration because Story 1.17 must leave those unchanged.
