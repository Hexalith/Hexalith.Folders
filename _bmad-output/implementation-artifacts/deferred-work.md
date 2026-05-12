# Deferred Work

This file accumulates items deferred from BMAD reviews and audits. Each section is dated and references its source story.

## Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)

- Error subtypes (`SafeAuthorizationDenial`, `ValidationFailure`, `IdempotencyConflict`, `ReconciliationRequired`) `allOf` `ProblemDetails` with no own discriminating properties (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:292-307`). Downstream stories 1.7-1.11 must specialize each with operation-relevant required fields.
- `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444`). Foundation vocabulary; downstream operation groups must `$ref` them when they emit operator-disposition or sensitivity-tagged data.
- `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag. Owned by story 1.12 (NSwag SDK generation) and story 1.14 (drift gate); validate when those stories land.
- CLI exit code → CanonicalErrorCategory mapping table is not declared. The 14-value `CliExitCode` enum exists but distinct categories like `response_limit_exceeded`, `query_timeout`, `redacted`, `client_configuration_error` have no exit-code assignment. Owned by story 1.13 (parity oracle).
- No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.
- `oidc.local.invalid` may hang on corporate DNS sinkholes that override RFC 2606. Affects only consumers that pre-fetch metadata at codegen time. Environmental edge case outside MVP scope.
- `Idempotency-Key` parameter is declared `required: true` globally as a reusable component. Downstream authors must explicitly not `$ref` it on query operations. Foundation note in `docs/contract/contract-spine-foundation.md:13` already states this; deferred to per-operation author discipline + future contract-completeness gate.

## Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)

- `<InternalsVisibleTo>` entries in `src/Hexalith.Folders.*/*.csproj` point to test assemblies (`Hexalith.Folders.*.Tests`) that didn't exist in commit `eb52d15`; they exist at HEAD as later commits added them. No action needed unless a test project is later removed.
- `Directory.Build.props:23-26` declares MSBuild properties `HexalithEventStoreRoot` and `HexalithTenantsRoot` that nothing currently consumes. Likely placeholders for future-story consumption (e.g., per-project file lists, NuGet feed switching). Revisit when a downstream story imports them.
- Predev preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer due to a dirty working tree (sprint-status + story 1-6 staged). Process concern outside the code-review scope — track via the preflight gate, not in this story.
- `.gitmodules` declares 5 root submodules including `Hexalith.Memories`, but `Directory.Build.props` only detects `Hexalith.EventStore` and `Hexalith.Tenants`. Add a `HexalithMemoriesRoot` detector when a downstream story first references Memories.
- No `Directory.Build.targets` adapted from `Hexalith.Tenants`. Acceptable deviation today; revisit when stories require SourceLink wiring or pack-time MSBuild logic.

## Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)

- Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated. Current approved course correction keeps Memories as an architecture-guided extension path.
- When a downstream story first implements Memories integration, add a dedicated story or story split for worker-owned semantic indexing:
  - worker-side `IFolderSemanticIndexingClient` port,
  - optional `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts` dependency only from `Hexalith.Folders.Workers`,
  - Folders-owned indexing bridge projection for `file version -> Memories workflow/memory unit/status`,
  - stable source URI/idempotency metadata,
  - explicit skipped/too-large/binary/excluded statuses,
  - authorized RAG query facade that applies tenant access, folder ACL, path policy, sensitivity classification, and C4 limits before calling Memories.
- If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.
- Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG response assembly in MVP.
