# Deferred Work

This file accumulates items deferred from BMAD reviews and audits. Each section is dated and references its source story.

## Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)

- `.editorconfig` `async_methods_should_end_with_async` rule may flag controller actions and Blazor lifecycle overrides at feature-implementation time — deferred to the first feature story that trips it. (`.editorconfig:41-49`)
- Private-field naming rule covers `private` accessibility only; `protected`/`internal` field naming silently allowed — deferred until those modifiers actually appear. (`.editorconfig:31-39`)
- `CA1062`, `CA2007` severities set to `warning` combined with root `TreatWarningsAsErrors=true` could mass-fail builds when real code lands. Builds pass today per Story 1.2 Dev Notes; revisit if a feature story trips it. (`.editorconfig:59-61`)
- Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md`. Drift risk but intentional per spec for discoverability. Revisit when an automated single-source-of-truth pattern (e.g., generated includes) becomes available.
- `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Revisit if a private feed becomes legitimate later.
- `Deterministic=true` paired with `ContinuousIntegrationBuild` gated to `'$(CI)' == 'true'` means local PDBs still carry absolute paths. Matches the gated intent; revisit if local-build determinism becomes a requirement.
- Story 1.2 spec File List (lines 240-247) omits `.gitmodules` from the touched files, even though `.gitmodules` was modified. Record-keeping inconsistency; sweep on next dev-record housekeeping pass.
- `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` now locks down the entire 24-project dependency graph — properly Story 1.1's territory and brittle. Acceptable per Story 1.2's "solution/dependency smoke test" allowance; revisit ownership in a Story 1.1 review iteration.

## Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)

- `ProductionUrl` regex in `ExitCriteriaDecisionArtifactTests` would reject legitimate documentation citations such as `https://learn.microsoft.com/...` if any are added later. No current usage; revisit if a citation outside the `.invalid` TLD becomes necessary. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:222-224`)
- Opaque / provider-token detection (PASETO, Macaroon, GitHub PATs as non-JWTs) — `RawJwt` only catches `eyJ`-prefixed JWTs. Tracked as part of the broader hygiene-scan vocabulary owned by story 1-6 follow-ups; not a story 1.4 concern.
- `File.ReadAllText` in the doc-verification test makes no encoding assertion — BOM / UTF-16 edge cases would silently misread the artifact. Low risk: project standardizes on UTF-8. Revisit if an editor introduces non-UTF-8 content.
- Windows case-insensitive filesystem path normalization in `ExitCriteriaDecisionArtifactTests` could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`). Convention is enforced by PR diff review; revisit if a regression appears.
- C6 transition-matrix mapping artifact maps every state to a single Story 4.1 consumer. If 4.1 splits, all rows will need re-pointing. Already captured in the artifact's `open questions` section. Defer to story 4.1 entry. (`docs/exit-criteria/c6-transition-matrix-mapping.md`)

## Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)

- Error subtypes (`SafeAuthorizationDenial`, `ValidationFailure`, `IdempotencyConflict`, `ReconciliationRequired`) `allOf` `ProblemDetails` with no own discriminating properties (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:292-307`). Downstream stories 1.7-1.11 must specialize each with operation-relevant required fields.
- `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444`). Foundation vocabulary; downstream operation groups must `$ref` them when they emit operator-disposition or sensitivity-tagged data.
- `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag. Owned by story 1.12 (NSwag SDK generation) and story 1.14 (drift gate); validate when those stories land.
- CLI exit code → CanonicalErrorCategory mapping table is not declared. The 14-value `CliExitCode` enum exists but distinct categories like `response_limit_exceeded`, `query_timeout`, `redacted`, `client_configuration_error` have no exit-code assignment. Owned by story 1.13 (parity oracle).
- No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.
- `oidc.local.invalid` may hang on corporate DNS sinkholes that override RFC 2606. Affects only consumers that pre-fetch metadata at codegen time. Environmental edge case outside MVP scope.
- `Idempotency-Key` parameter is declared `required: true` globally as a reusable component. Downstream authors must explicitly not `$ref` it on query operations. Foundation note in `docs/contract/contract-spine-foundation.md:13` already states this; deferred to per-operation author discipline + future contract-completeness gate.

## Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)

- Simultaneous-cancellation TOCTOU in `Eventually.UntilAsync`: when both `timeoutSource.Token` and the caller's `cancellationToken` fire in the same quantum, the `when` filter evaluates `false` and raw `OperationCanceledException` propagates instead of `TimeoutException`. Low probability for a testing utility; acceptable trade-off. (`src/Hexalith.Folders.Testing/Polling/Eventually.cs`)
- `TaskId`, `CorrelationId`, `IdempotencyKey` not validated at `TestFolderContext` construction — intentional asymmetric design: stream-segment fields validated early; header fields validated at header-build time by `ValidateHeaderValue`. (`src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`)
- `"diff --git"` in `CredentialMaterialMarkers` is overly broad; would false-positive on any fixture legitimately containing a diff snippet. Pre-existing marker moved from old `ShouldNotContain` calls; no current fixture affected. (`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs`)
- `TestFolderContext` changed from positional record to custom-constructor record — loses compiler-synthesised positional deconstruction and `System.Text.Json` deserialisation support without a custom converter. Testing helper; neither usage pattern expected. (`src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`)
- `RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption` test outcome is correct, but the `proseLinesSeen == 1` early-break prevents line 2 from ever being evaluated — the test does not mechanically verify the claimed "broad wording is rejected" path. (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`)
- A probe's own `OperationCanceledException` (from an unrelated internal token) may be misattributed as a timeout when `timeoutSource.IsCancellationRequested` is simultaneously true. Very low probability for a test utility. (`src/Hexalith.Folders.Testing/Polling/Eventually.cs`)
- `CollectPrecedingProseContext` early-break limits exemption window to the immediately preceding prose line — intentional per 2026-05-12 review finding ("immediate preceding prose must carry the warning"); a warning comment separated from the recursive command by one non-warning line will not be seen. (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`)

## Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)

- `C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally` checks backtick-wrapped event names in architecture.md — tests pass because events appear backtick-wrapped in prose, but the canonical transition table uses bare cell names; design nuance, not a failure. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
- Dynamic `last reviewed` date in row-date assertions changes failure semantics from hard-coded `"2026-05-11"` to front-matter-derived date — intentional improvement; undocumented scope change. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
- `"diff --git"` in `SecretSubstringDenylist` alongside credential patterns produces confusing diagnostic; intent is correct (no patch content in docs) but classification is misleading. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
- `RepositoryRoot` `MaxAncestors = 12` magic number; could throw `InvalidOperationException` on deeply nested CI paths. 12 is sufficient for known repo layouts; pre-existing design.
- S2 OIDC test split into `S2OidcArtifactPinsFrozenJwtBearerSettings` and `S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders` — full OIDC contract only visible across both tests; structural coupling concern, both pass.

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
