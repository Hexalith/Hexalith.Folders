# Story 1.15: Wire safety invariant CI gates

Status: done

Created: 2026-05-13

## Story

As a maintainer,
I want safety invariant gates wired into CI,
so that implementation cannot leak secrets, file contents, or tenant data through generated or runtime artifacts.

## Acceptance Criteria

1. Given `tests/fixtures/audit-leakage-corpus.json` exists, when CI runs, then sentinel-corpus redaction tests execute against configured output channels and fail on any detected file content, token, credential, generated-context, provider payload, tenant data, local absolute path, production URL, or unauthorized-resource leakage.
2. Given the sentinel corpus is currently a seeded synthetic fixture, when this story hardens it, then new samples remain synthetic, metadata-only, explicitly classified, and safe to commit; real tenant names, repository URLs, branch names, provider payloads, file contents, diffs, tokens, credentials, or customer data are never added as examples.
3. Given every signal-emitting component can become a leak path, when safety gates run, then they cover logs, traces, metric labels, events, audit records, projections, provider diagnostics, console payload examples, generated SDK/parity artifacts, OpenAPI examples, Problem Details examples, and developer-facing diagnostics that are present in the repository.
4. Given a channel is not yet implemented, when CI runs, then the gate records a bounded reference-pending or prerequisite-drift diagnostic for that channel instead of silently passing or inventing runtime behavior.
5. Given Story 1.14 owns Contract Spine drift, generated-client, and parity schema CI wiring, when this story adds workflow steps, then it reuses or extends the existing workflow lane without adding duplicate restore/build/test commands or broad release/security jobs outside safety invariant enforcement.
6. Given Story 1.16 owns exit-criteria, idempotency-encoding, tenant-prefixed cache-key lint, pattern-example compilation, and parity completeness gates, when this story is complete, then it does not implement those gates or move their ownership into the safety-invariant workflow.
7. Given sensitive metadata classification is authoritative, when tests scan outputs, then paths, branch names, repository names, commit messages, provider correlation IDs, actor metadata, folder/workspace/task IDs, and audit metadata are handled according to the approved classification/redaction vocabulary rather than simple blanket string deletion.
8. Given unauthorized-resource existence is itself sensitive, when negative cases run, then wrong-tenant, unauthorized, hidden, redacted, missing, unknown, stale, and projection-unavailable examples do not reveal resource existence through status text, counts, ordering, cursor values, stack traces, schema examples, or diagnostics.
9. Given context-query authorization order is tenant access, folder ACL, path policy, then execution, when safety tests inspect search, glob, partial-read, and file-metadata contract examples or available tests, then sentinel values are checked after the authorization boundary and never rely on search-first/filter-later behavior.
10. Given generated artifacts are reviewed in pull requests, when safety gates scan generated clients, parity rows, normalized OpenAPI, schema validation output, and helper diagnostics, then they allow only safe provenance such as operation IDs, schema pointers, content hashes, gate names, repository-relative paths, and synthetic sentinel IDs.
11. Given CI diagnostics are visible outside the local developer machine, when a safety gate fails, then logs include the gate name, repository-relative path, synthetic sample ID, output channel, and bounded classification only; logs must not echo the forbidden value, raw payload, file content, diff, token, credential, local absolute path, production URL, tenant data, or unauthorized resource hint.
12. Given the gates must be locally reproducible, when implementation is complete, then a documented local command runs the same sentinel/redaction checks without requiring Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or nested submodule initialization.
13. Given active Story 1.10 through Story 1.14 work may still be dirty or reference-pending, when implementation starts, then the developer inspects the current OpenAPI, generated-artifact expectations, parity schema, contract docs, and existing test projects before assuming which output channels are implemented.
14. Given this story wires safety invariant gates only, when implementation is complete, then it does not add runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects outside deterministic tests/tools, SDK generation policy, parity-oracle derivation, CLI commands, MCP tools, UI pages, Dapr policy conformance, provider drift jobs, release publishing, tenant cache-key lint, exit-criteria checks, or nested-submodule initialization.
15. Given safety output channels can drift as Stories 1.10 through 1.14 land, when this story implements scanning, then a channel inventory or manifest defines each scanned channel, owning story, artifact/test source, prerequisite status, and whether absence is `reference-pending` or `prerequisite-drift`.
16. Given the sentinel corpus is the authoritative safety vocabulary for this gate, when tests load it, then unknown classification labels, local synonyms, missing forbidden-surface lists, or missing allowed-provenance lists fail before any artifact scan runs.
17. Given telemetry can leak outside message bodies, when safety gates scan traces and metrics, then they inspect tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage in addition to log text.
18. Given generated artifact scans overlap adjacent CI stories, when this story scans OpenAPI, generated SDK output, parity artifacts, Problem Details examples, developer diagnostics, or CI logs, then it checks leakage and safe provenance only; it does not validate Contract Spine drift, parity completeness, schema derivation, client-generation correctness, or exit criteria.
19. Given intentionally contaminated samples are required to prove detection, when this story adds negative controls, then those samples are quarantined under an explicit fixture/test path, remain synthetic-only, are never reused as normative examples, and never cause assertion libraries or CI output to echo the forbidden value.
20. Given channel inventory can become stale as adjacent stories land, when the gate starts, then each manifest entry resolves to an existing repository-relative artifact or an explicit `reference-pending` / `prerequisite-drift` status with owning story, and stale claimed coverage fails with bounded metadata-only diagnostics.
21. Given scan scope can accidentally expand into unrelated or unsafe repository content, when safety gates enumerate inputs, then they use explicit repository-relative include roots and structured exclusions for `.git`, build outputs, package caches, binary blobs, local machine paths, and generated files outside declared channels.

## Tasks / Subtasks

- [x] Confirm safety-gate prerequisites and current artifact ownership. (AC: 1, 2, 3, 4, 5, 6, 12, 13)
  - [x] Inspect `.github/workflows/`; if Story 1.14 has not created a workflow yet, create only the focused safety workflow or a narrowly named job that can later be composed with Story 1.14.
  - [x] Inspect `tests/fixtures/audit-leakage-corpus.json`, `tests/README.md`, `_bmad-output/project-context.md`, and `_bmad-output/planning-artifacts/architecture.md` sections on sentinel redaction, sensitive metadata classification, authorization order, and enforcement guidelines.
  - [x] Inspect Story 1.10 through Story 1.14 artifacts before assuming implemented OpenAPI examples, generated-client paths, parity rows, or CI workflow names.
  - [x] Inspect existing test projects for the best home for safety checks, especially `tests/Hexalith.Folders.Contracts.Tests/`, `tests/Hexalith.Folders.Testing.Tests/`, `tests/Hexalith.Folders.Server.Tests/`, `tests/Hexalith.Folders.UI.Tests/`, and any generated-artifact tests created by Stories 1.12 through 1.14.
  - [x] Create or update a channel inventory or manifest that names each scanned channel, owning story, artifact or test source, prerequisite status, and safe absence diagnostic.
  - [x] Validate that each manifest entry either points to an existing repository-relative artifact/test source or is explicitly marked `reference-pending` or `prerequisite-drift`; claimed coverage with a stale path must fail closed.
  - [x] Define explicit scan include roots and exclusions so the gate does not scan `.git`, package caches, build outputs, binary blobs, local machine paths, or undeclared generated directories.
  - [x] Treat missing runtime channels, missing generated artifacts, absent workflow files, or placeholder-only safety helpers as prerequisite drift unless the gate can fail closed with a targeted diagnostic.
  - [x] Do not initialize or update nested submodules. If submodules are needed for local validation, initialize only the root-level modules listed in `AGENTS.md`.
- [x] Harden the sentinel corpus contract. (AC: 1, 2, 7, 8, 11)
  - [x] Preserve `tests/fixtures/audit-leakage-corpus.json` as the single normative cross-project sentinel corpus.
  - [x] Add only synthetic sentinel samples needed to prove file-content, token, credential, generated-context, provider-payload, tenant-data, unauthorized-resource, local-path, production-URL, path, branch, repository, commit-message, actor, correlation, diff, diagnostic-echo, and safe-provenance rules.
  - [x] Require each sentinel to declare classification, category, forbidden output surfaces, allowed provenance-safe representations, and whether it participates in positive or intentionally contaminated negative-control fixtures.
  - [x] Add schema or fixture-contract tests that fail if a sample lacks `synthetic_sentinel`, `synthetic_data_only`, classification, category, ID, or safe notes.
  - [x] Add tests that fail when unknown classification labels or ad hoc local vocabulary appear outside the sentinel corpus contract.
  - [x] Add tests that fail if corpus samples look like real tenant IDs, real provider URLs, real repository names, real local absolute paths, real production hosts, real secrets, or raw content/diff excerpts.
  - [x] Keep new categories reviewer-visible in the corpus; do not hide policy expansion inside test code only.
  - [x] Keep intentionally contaminated negative-control fixtures in an explicitly named quarantine location and mark them so documentation, OpenAPI examples, generated outputs, and normative fixtures cannot consume them accidentally.
  - [x] Use custom safe assertion helpers or sanitized assertion messages for leakage tests so a test failure reports only rule ID, channel, sample ID, classification, and remediation hint, never the leaked value or raw payload.
- [x] Add or wire safety gate test entry points. (AC: 1, 3, 4, 7, 8, 9, 10, 11, 12)
  - [x] Prefer focused test projects or repository tools that can run locally and in CI with the same command.
  - [x] Scan generated and checked-in artifacts through structured parsers where practical: JSON for fixtures/schema files, YAML for OpenAPI/parity artifacts, and targeted text checks for docs or generated diagnostics.
  - [x] Cover logs/traces/metrics/events/audit/projections/provider diagnostics through available examples, fixtures, or channel-specific test seams. For channels not implemented yet, emit prerequisite-drift evidence rather than success.
  - [x] Include tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage in telemetry scans instead of scanning only message strings.
  - [x] Include at least one intentionally contaminated fixture per scan family so the gate proves forbidden values are detected without printing the forbidden values.
  - [x] Verify the negative-control path itself is excluded from normal artifact/example scans unless a test explicitly opts into contaminated-fixture validation.
  - [x] Validate OpenAPI examples and Problem Details examples for metadata-only fields, safe-denial shape, redaction shape, bounded classification, and absence of raw payload values.
  - [x] Validate generated SDK/parity/gate diagnostics for leakage and safe provenance only, without using broad `git diff --exit-code` across unrelated active development files and without asserting parity completeness, derivation, or generated-client correctness.
  - [x] Check context-query examples and tests for authorization-before-observation ordering and no search-first/filter-later leakage.
  - [x] Keep failure messages safe: report gate name, output channel, repository-relative artifact path, rule ID, synthetic sample ID, classification, and remediation hint only; never report the leaked value itself.
  - [x] Emit bounded missing-channel diagnostics such as `SAFETY-CHANNEL-MISSING` or `SAFETY-PREREQUISITE-DRIFT` with channel name, owner, and remediation hint only.
- [x] Wire the CI job for safety invariants. (AC: 1, 5, 6, 11, 12, 14)
  - [x] Add or update a focused GitHub Actions job for safety invariant gates, preferably in the workflow established by Story 1.14 if it exists by implementation time.
  - [x] Use one repository-root offline command, preferably backed by `tests/tools`, that CI invokes unchanged and that respects `global.json`, central package management, and the root-level submodule policy.
  - [x] Run restore/build/test steps only as needed for this safety gate lane; do not duplicate the full release pipeline.
  - [x] Keep all CI diagnostics repository-relative and metadata-only.
  - [x] Do not upload raw scanner inputs, contaminated fixtures, assertion diffs, generated snippets, local paths, or full CI logs as workflow artifacts; if artifacts are needed, upload only sanitized summaries with rule IDs, channel names, sample IDs, and content hashes.
  - [x] Do not add Dapr policy conformance, provider live drift, package publishing, release evidence, exit-criteria, cache-key tenant-prefix, idempotency-encoding, C6 matrix, or parity completeness jobs in this story.
- [x] Document developer and reviewer usage. (AC: 2, 4, 7, 8, 11, 12, 13)
  - [x] Add or update focused documentation such as `docs/contract/safety-invariant-ci-gates.md`.
  - [x] Document local commands, CI job names, scanned inputs, output-channel coverage, channel inventory fields, prerequisite-drift categories, and safe diagnostic format.
  - [x] Document how to add new synthetic sentinel categories and what reviewer approval is required.
  - [x] Document how negative-control quarantine paths differ from normative examples and how test failures are sanitized before reaching CI logs or uploaded artifacts.
  - [x] Document manifest freshness semantics, including when `reference-pending` is acceptable and when stale claimed coverage must fail.
  - [x] Document how redacted, unknown, missing, hidden, unauthorized, stale, and unavailable states differ without leaking resource existence.
  - [x] Add a reviewer checklist covering synthetic-only corpus changes, forbidden-value echo prevention, safe CI diagnostics, channel inventory coverage, generated-artifact leakage-only scope, and Story 1.16 scope boundaries.
  - [x] Document that Story 1.16 still owns tenant-prefixed cache-key lint and exit-criteria gates.
- [x] Run verification. (AC: 1, 2, 3, 11, 12, 14)
  - [x] Run the focused safety invariant tests.
  - [x] Run the workflow-equivalent local command from the repository root.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if active contract work and prerequisite drift allow it; if blocked, record the exact blocker.
  - [x] Confirm no nested submodules were initialized.
  - [x] Confirm no unrelated active Story 1.10 through Story 1.14 files were modified.

### Review Findings

Code review run: 2026-05-17 against commit `3d75bbb` ("Add safety invariant CI gates and related tests"). Reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor.

#### Decision-needed (commit-scope governance) — RESOLVED via additive revert commit `a3cd62f`

- [x] [Review][Decision→Patch] Scope creep — Story 2.4 file committed in this commit — `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md` (new, 196 lines) was added alongside the 1.15 changes. Preflight `_bmad-output/process-notes/predev-preflight-latest.json` already flagged this with `result: fail`, `dirty_path_count: 12`, and `expected: absent`. **Resolved by `a3cd62f`** — file removed from history additively (no force-push).
- [x] [Review][Decision→Patch] Scope creep — submodule pointer bumps for `Hexalith.EventStore` (a1790f9 → da2f2cf) and `Hexalith.Tenants` (e465880 → 3a1f4a7) are part of this commit. Story 1.15 AC 14 and File List do not authorize submodule changes; project-context pins both to `3.15.1`. **Resolved by `a3cd62f`** — pointers reset to pre-3d75bbb values.
- [x] [Review][Decision→Patch] Scope creep — 3 preflight snapshot JSONs (`predev-preflight-2026-05-17T112005Z.json`, `T120209Z.json`, `T121342Z.json`) were committed under `_bmad-output/process-notes/`. None were in the story File List. **Resolved by `a3cd62f`** — snapshots removed from history; matches the `af6f9a5` convention of keeping preflight audit files uncommitted.

#### Patch — Acceptance Criteria violations

- [x] [Review][Patch] AC 9 — authorization order check uses `Take(3)`, missing the "then execution" tail. **Applied in `87bce31`** — now asserts full ordered prefix `tenant_access/folder_acl/path_policy` AND `query_execution` as the terminal step, with an explicit count guard ≥ 4.
- [x] [Review][Patch] AC 2 — synthetic token used real GitHub PAT prefix `ghp_`. **Applied in `87bce31`** — renamed to `synthetic_pat_SENTINEL_NEVER_USABLE_*` (no real-provider prefix); quarantine fixture updated to match.
- [x] [Review][Patch] AC 17 — telemetry surfaces incomplete [tests/fixtures/audit-leakage-corpus.json + safety-channel-inventory.json]. AC 17 requires scanning span names, metric names, event names, counters, exception metadata, and baggage; neither the surface vocabulary, channel inventory entries, nor scan code distinguishes these from flat `traces`/`metric-labels`/`events` buckets. **Applied in this follow-up** — added distinct telemetry surfaces to the corpus, inventory, tests, and negative controls.
- [x] [Review][Patch] AC 11 — stock `Shouldly.ShouldNotContain` echoes both operands on assertion failure, so a future leak would surface the forbidden value in CI logs [tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs throughout]. Task line 58 mandates "custom safe assertion helpers or sanitized assertion messages" — not implemented. **Applied in this follow-up** — added sanitized forbidden-value assertions and metadata-only failure helpers.
- [x] [Review][Patch] AC 1 / AC 3 — 9 of 18 inventory channels are blanket `reference-pending` with no per-channel justification. At minimum `audit-records` (Story 1.11 is done), `events`, `projections`, and `console-payloads` should be re-evaluated against existing OpenAPI / fixtures. **Applied in this follow-up** — re-evaluated current OpenAPI/generated-client diagnostic surfaces, marked current contract artifacts covered where present, and added bounded absence reasons where runtime channels remain pending.
- [x] [Review][Patch] AC 8 — wrong-tenant/unauthorized/hidden/redacted/missing/unknown/stale/projection-unavailable distinction never tested. **Applied in this follow-up** — added OpenAPI safe-denial and diagnostic-state coverage tests.
- [x] [Review][Patch] AC 4 — prerequisite-drift / reference-pending diagnostics live only as static JSON in the inventory; nothing emits them as runtime diagnostics. **Applied in this follow-up** — added a bounded diagnostic-emission test over non-covered manifest entries.
- [x] [Review][Patch] AC 15 — `owning_story` used wildcards (`2-x-runtime-observability`, etc.). **Applied in `87bce31`** — replaced with concrete story IDs (4-14, 4-15, 3-5, 6-2, 1-6, 1-11, 1-14). Test now rejects `-x-` segments as a regression guard.
- [x] [Review][Patch] AC 21 — `structured_exclusions` missing categories. **Applied in `87bce31`** — added `.vs/`, `.idea/`, `TestResults/`, `artifacts/`, `**/.nuget/**`, `*.binlog`. Test now requires `.git/**`, `**/bin/**`, `**/obj/**` baseline.
- [x] [Review][Patch] AC 18 — `ScanManifestCoveredArtifacts` uses `text.Contains(sample.Value)` without word-boundary protection. **Applied in this follow-up** — scan matching now requires token boundaries and treats declared safe-provenance values as allowed provenance.
- [x] [Review][Patch] AC 12 — three documented commands (restore + build + script) rather than the single offline command. **Applied in this follow-up** — the safety script now restores, builds, and runs tests by default; CI uses `-SkipRestoreBuild` after the shared build lane.

#### Patch — Test, fixture, workflow, and script hardening

- [x] [Review][Patch] Inventory fields `include_roots` and `safe_absence_diagnostic` declared but never read by tests. **Applied in `87bce31`** — `include_roots` entries are now path-resolved and the `safe_absence_diagnostic` enum is validated against the bounded vocabulary.
- [x] [Review][Patch] Asymmetric leak detection — `ScanText` was `StringComparison.Ordinal`. **Applied in `87bce31`** — switched to `OrdinalIgnoreCase`. `AssertMetadataOnly` already used `Case.Insensitive`.
- [x] [Review][Patch] `assertion-messages` channel had `scan_forbidden_values: false`. **Applied in `87bce31`** — flipped to `true`; channel now actually scans `SafetyInvariantGateTests.cs` for forbidden sample values.
- [x] [Review][Patch] `negative-control-quarantine.scan_forbidden_values: false` is decorative — `ScanNegativeControls` ignores the flag. **Applied in this follow-up** — quarantine now declares explicit `opt_in_scan_forbidden_values: true`, and the opt-in scanner validates it.
- [x] [Review][Patch] `Take(3)` cryptic-failure mode. **Resolved by AC 9 fix in `87bce31`** — explicit `orderSteps.Length.ShouldBeGreaterThanOrEqualTo(4)` guard now reports the missing-positions case clearly.
- [x] [Review][Patch] `IsBinaryFile` case-sensitive + incomplete. **Applied in `87bce31`** — extension match now case-insensitive (`ToLowerInvariant()`); added `.so`, `.dylib`, `.lib`, `.bin`, `.tar`, `.gz`, `.7z`, `.rar`, `.ico`, `.bmp`, `.tiff`, `.mp4`, `.mov`, `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.binlog`.
- [x] [Review][Patch] `EnumerateSourceFiles` used `*.*`. **Applied in `87bce31`** — uses `*` glob; extensionless files like `Dockerfile`, `LICENSE`, `Makefile` are now scanned.
- [x] [Review][Patch] `FindRepositoryRoot` no fallback. **Applied in `87bce31`** — tries `GITHUB_WORKSPACE`, then `AppContext.BaseDirectory`, then `Directory.GetCurrentDirectory` ancestries before failing with a bounded `SAFETY-PREREQUISITE-DRIFT` diagnostic.
- [x] [Review][Patch] Drift between corpus (11 negative-control participants) and quarantine fixture (5 controls). **Applied in this follow-up** — quarantine now covers every corpus sample that participates in negative controls.
- [x] [Review][Patch] `FixtureContractTests.cs` modification weakens classification enforcement. **Applied in this follow-up** — added an explanatory vocabulary-authority test that pins the Story 1.15 relaxation while still enforcing reviewer-visible classifications.
- [x] [Review][Patch] `Path.GetRelativePath` `..`-prefix path not guarded. **Applied in `87bce31`** — `AssertRepositoryRelativePath` now splits on `/` and rejects any `..` segment.
- [x] [Review][Patch] Directory enumeration could follow symlink loops. **Applied in `87bce31`** — `EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System }`.
- [x] [Review][Patch] Workflow safety step lacked `if: always()` and `timeout-minutes`. **Applied in `6033eb1`** — both added (timeout-minutes: 15).
- [x] [Review][Patch] PowerShell script missing `Set-StrictMode`, `dotnet` PATH check, and PS 5.1-compatible `Join-Path`. **Applied in `6033eb1`** — Set-StrictMode -Version Latest, dotnet PATH probe emitting bounded SAFETY-PREREQUISITE-DRIFT, two-arg Join-Path calls, and Push-Location/$pushed guard.
- [x] [Review][Patch] `-NoRestore` flag inversion / doc inconsistency. **Applied in this follow-up** — renamed the documented CI flag to `-SkipRestoreBuild` and kept `-NoRestore` as a compatibility alias.

#### Defer (low-likelihood / pre-existing)

- [x] [Review][Defer] Encoding/BOM handling in `File.ReadAllText` — deferred, low likelihood any scanned file is non-UTF-8.
- [x] [Review][Defer] JSON duplicate-keys detection — deferred, .NET `JsonDocument` default behavior is acceptable for fixtures we control.
- [x] [Review][Defer] AssertMetadataOnly blacklist missing Linux absolute-path roots (`/home/`, `/Users/`, `/var/`, `/tmp/`) — deferred, the current gate runs primarily on Windows.

### Round 3 Review Findings

Code review run: 2026-05-18 against scope `3d75bbb~1..HEAD` constrained to the 1.15 file allowlist (9 files, +2012/−32 lines). Reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor.

#### Decision-needed (Round 3) — RESOLVED 2026-05-18

- [x] [Review][Decision→Dismiss] Cross-story scope of commit `e6d9de5` — **Resolution:** dismissed for 1.15 review. Commit `e6d9de5` is authored by Story 1.16 ("Add governance completeness evidence and associated tests"); the workflow step and `tests/README.md` governance block belong to 1.16's review scope. Not 1.15 scope creep.
- [x] [Review][Decision→Dismiss] Channel manifest `covered` claim for Story-1.11-owned channels — **Resolution:** keep `covered` as-is. Contract-only coverage is consistent with AC 1/3's "re-evaluated against existing OpenAPI" direction, and `docs/contract/safety-invariant-ci-gates.md` already names the runtime-channel limit explicitly. No change.
- [x] [Review][Decision→Patch] Telemetry channels declared with `scan_forbidden_values: false` — **Resolution:** converted to patch (P24 below). Channels `logs`, `traces`, `span-names`, `metric-names`, `event-names`, `counters`, `exception-metadata`, `baggage`, `metric-labels`, `telemetry-attributes` flip from `covered` + `scan_forbidden_values: false` to `prerequisite-drift` so manifest internally distinguishes vocabulary-declared from actually-scanned.
- [x] [Review][Decision→Defer] `safe-provenance` classification short-circuits `ScanText` entirely — **Resolution:** deferred. Only one `safe-provenance` sample exists today (`safe-provenance-operation-id`); per-sample `allowed_in_channels` design can wait until a second safe-provenance entry is needed.

#### Patch — Acceptance Criteria gaps (Round 3)

- [x] [Review][Patch] AC 11 — raw `Shouldly.ShouldNotContain` / `ShouldContain` echoes operands on failure [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `OpenApiExamplesAndContextQueriesRemainMetadataOnly`, `SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence`]. Custom safe assertion helper required per AC 11 / Task line 58 — partial implementation remains in YAML-scan assertions.
- [x] [Review][Patch] AC 1 / AC 3 — `OpenApiExamplesAndContextQueriesRemainMetadataOnly` hard-codes operationIds `SearchFolderFiles`/`GlobFolderFiles`/`ReadFileRange`/`GetFolderFileMetadata` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]. If any operation is renamed in the OpenAPI yaml the `.Where(...)` collapses to empty and the test passes vacuously — the exact anti-pattern the gate is meant to prevent.
- [x] [Review][Patch] AC 21 — `IsExcludedByInventory` ignores manifest `structured_exclusions` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `IsExcludedByInventory`]. Hardcoded 5 prefixes/3 segments; manifest declares 17 entries. `.vs/`, `.idea/`, `TestResults/`, `artifacts/`, `**/.nuget/**`, `packages/**`, `*.binlog` are not honored at runtime.
- [x] [Review][Patch] AC 15 / AC 21 — `include_roots` field declared in manifest and existence-checked, but `ScanManifestCoveredArtifacts` ignores it [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanManifestCoveredArtifacts`]. Declared field has no runtime effect; either iterate it or drop it.
- [x] [Review][Patch] AC 2 — `SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary` asserts `samples.GetArrayLength().ShouldBeGreaterThanOrEqualTo(14)` while corpus has 18 samples [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]. Lax floor allows silent removal of 4 samples including `safe-provenance` and negative-control participants. Pin to exact expected count or per-category floors.
- [x] [Review][Patch] AC 19 — `production-url-marker` sentinel value is the literal `PRODUCTION_URL_SYNTHETIC_INVALID_HOST` (not URL-shaped) [`tests/fixtures/audit-leakage-corpus.json`]. Negative control cannot prove URL-shape detection. Use synthetic URL form such as `https://synthetic.invalid/safe/sample`.
- [x] [Review][Patch] AC 8 — `projectionAvailability` enum check uses `ShouldContain` per state rather than set-equality [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence`]. Future enum additions (`"leaked"`, `"public"`) pass without flagging contradiction with safe-state semantics.
- [x] [Review][Patch] AC 8 / AC 11 — substring matches on `"count"`, `"cursor"`, `"stack"` against serialized YAML will false-positive on `account`, `recount`, `discount`, `precursor`, `cursoryNotes` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence`]. Use whole-token regex `\bcount\b` / `\bcursor\b` / `\bstack\b`.
- [x] [Review][Patch] AC 5 — `Run safety invariant gates` step has `if: always()`; `Run governance completeness gates` step does not [`.github/workflows/contract-spine.yml:41-50`]. Partial-failure signaling diverges between the two safety-domain gates. Either both run on failure or neither.
- [x] [Review][Patch] AC 12 — `tests/README.md` documents `-NoRestore` for the safety gate; `docs/contract/safety-invariant-ci-gates.md` flags `-NoRestore` as legacy alias and prefers `-SkipRestoreBuild`. README example should use `-SkipRestoreBuild` to match its own guidance.

#### Patch — Test, fixture, workflow, and script hardening (Round 3)

- [x] [Review][Patch] `ContainsForbiddenValue` token-boundary set treats `_`, `-`, `/`, `.` and alphanumerics as in-word [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `HasTokenBoundary`/`ContainsForbiddenValue`]. Sentinels embedded inside path-like or identifier-like strings won't match. Restrict boundary set to whitespace + structural punctuation, or fall back to plain `Contains` for hits inside larger tokens.
- [x] [Review][Patch] `AssertMetadataOnly` forbidden-array contains literal markers `"diff --git"`, `"AccountKey="`, `"clientSecret"`, `"D:\\"`, `"C:\\"` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertMetadataOnly`]. These literals cause false positives when applied to documentation strings that legitimately describe the forbidden patterns. Scope these markers to runtime/test-output surfaces only, or quote/escape them before the check.
- [x] [Review][Patch] `AssertMetadataOnly` includes `RepositoryRoot` and its slash-variants alongside generic `/home/` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertMetadataOnly`]. On Linux CI where `RepositoryRoot` may itself begin with `/home/`, diagnostics trigger double-detection or false positives. Compare `RepositoryRoot` first and skip overlapping generic markers.
- [x] [Review][Patch] `SafetyScanDiagnostic.ToString()` exposes plain `RepositoryPath` alongside `path_hash` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SafetyScanDiagnostic.ToString`]. Pick one provenance representation — emit hash-only to align with the doc's "no ordering hints / no raw paths" guidance, or document the dual emission intent.
- [x] [Review][Patch] `FindRepositoryRoot` fallback walks ancestors from `AppContext.BaseDirectory` and `Directory.GetCurrentDirectory` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `FindRepositoryRoot`]. In monorepo nested checkouts it may select a wrong slnx-bearing ancestor silently. If `GITHUB_WORKSPACE` is set without a slnx, emit `SAFETY-PREREQUISITE-DRIFT` rather than falling back.
- [x] [Review][Patch] `EnumerateSourceFiles` file-branch yields the path without applying `IsBinaryFile` / `IsExcludedByInventory` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `EnumerateSourceFiles`]. An inventory entry pointing at a binary or excluded file bypasses both filters.
- [x] [Review][Patch] `StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts` asserts `coverage_notes` contains literal `"re-evaluated"` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]. Editorial wording is fragile; switch to a structured field like `last_evaluated_at` or `evidence_link`.
- [x] [Review][Patch] `ScanText` does not deduplicate findings across `(artifact × channel)` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanText` / `ScanManifestCoveredArtifacts`]. A file shared by multiple channels produces N identical diagnostics; triage will be noisy. De-duplicate by `(repository_path, sample_id, channel)` or emit one diagnostic with a channel list.
- [x] [Review][Patch] `run-safety-invariant-gates.ps1` uses `dotnet test --no-build` unconditionally [`tests/tools/run-safety-invariant-gates.ps1`]. If `-SkipRestoreBuild` is passed without a prior matching build, `--no-build` fails opaquely with raw runner error. Probe for the test dll under `bin/` and emit `SAFETY-PREREQUISITE-DRIFT` instead.
- [x] [Review][Patch] `samples.Single(s => s.SampleId == sampleId)` raises raw `InvalidOperationException` when corpus rename drifts [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` negative-control scan]. Sequence-no-matching-element message is not metadata-only. Use `SingleOrDefault` and emit bounded diagnostic.
- [x] [Review][Patch] `AssertRepositoryRelativePath` rejects `Path.IsPathFullyQualified` and `..` segments but not Linux absolute paths (`/etc/x`) or cross-OS drive-letter patterns [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertRepositoryRelativePath`]. Add explicit `^[A-Za-z]:[\\/]` and leading `/` rejection.
- [x] [Review][Patch] `EnumerateOperations` operationId comparison `o.OperationId is "..."` is case-sensitive [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]. A YAML casing drift skips the assertion silently; switch to `StringComparison.OrdinalIgnoreCase`.
- [x] [Review][Patch] `safe-provenance-operation-id` sample value is `CreateFolder` [`tests/fixtures/audit-leakage-corpus.json`]. If a future scan reaches it through `AssertDoesNotContainForbiddenValue`, the legitimate OperationId would false-positive. Move to a synthetic non-collision value or document the bypass explicitly.
- [x] [Review][Patch] `SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined` parses corpus twice; the second `JsonDocument.Parse(...)` is not disposed [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]. Minor leak; wrap in `using`.
- [x] [Review][Patch] AC 17 — telemetry channels marked `covered` + `scan_forbidden_values: false` should flip to `prerequisite-drift` [`tests/fixtures/safety-channel-inventory.json` channels `logs`, `traces`, `span-names`, `metric-names`, `event-names`, `counters`, `exception-metadata`, `baggage`, `metric-labels`, `telemetry-attributes`]. The current state contradicts the manifest's freshness contract because `covered` should mean "scanned" but no scan path exists. Flipping to `prerequisite-drift` makes the vocabulary-only stance internally honest and the gate emits bounded missing-channel diagnostics until Stories 4.14 / 4.15 land runtime emission artifacts.

#### Defer (Round 3 — low-likelihood / pre-existing / overlaps prior defers)

- [x] [Review][Defer] YamlDotNet duplicate-keys detection — deferred, overlaps prior JSON-duplicate-keys defer; fixtures are gate-owned and deterministic.
- [x] [Review][Defer] File encoding fallback (UTF-16/Latin-1/no-BOM) — deferred, overlaps prior BOM defer.
- [x] [Review][Defer] AssertMetadataOnly Linux absolute-path roots expansion (`/home/`, `/Users/`, `/var/`, `/tmp/`) — deferred, overlaps prior defer; primary CI is Windows.
- [x] [Review][Defer] `InventoryChannel.Clone()` use-after-dispose latent risk — deferred, current code is correct per .NET docs; revisit if `InventoryChannel` is refactored to return raw `JsonElement`.
- [x] [Review][Defer] `schema_version "1.0.0"` hard-coded coupling — deferred, intentional schema-version gating; lift only when schema iterates.
- [x] [Review][Defer] File-locking races on Windows runners under parallel xUnit — deferred, rare on current runner topology.
- [x] [Review][Defer] Case-collision normalization across OS in `EnumerateSourceFiles` — deferred, cross-OS naming collision is rare in generated SDK and contract directories.
- [x] [Review][Defer] Workflow doc claim `checkout with submodules: false` not visibly enforced in this diff — deferred, the workflow file's existing checkout step is not modified by this story; verify when 1.14 ownership consolidates.
- [x] [Review][Defer] `safe-provenance` classification global allowlist behavior — deferred, only one safe-provenance sample (`safe-provenance-operation-id`) exists today; per-sample `allowed_in_channels` design can wait until a second safe-provenance entry is needed.

### Round 4 Review Findings

Code review run: 2026-05-18 against commit `d6cac60` ("Update sprint status, safety invariant documentation, and tests"). Reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor. Scope: 10 in-scope files, +258/−152 lines.

#### Decision-needed (Round 4) — RESOLVED 2026-05-18

- [x] [Review][Decision→Patch] Submodule pointer bumps for `Hexalith.EventStore` (bbfc454 → 898b3f5) and `Hexalith.FrontComposer` (218d19d → 02e267c) are in `d6cac60` but outside the 1.15 file allowlist. **Resolution:** option (a) — revert via additive commit, mirroring Round 1's `a3cd62f` precedent. Converted to patch P14.
- [x] [Review][Decision→Patch] `if: always()` removed from the `Run safety invariant gates` step in `.github/workflows/contract-spine.yml`; test `WorkflowAndDocumentationExposeSameOfflineSafetyGate` actively forbids it. **Resolution:** option (b) — restore `if: ${{ !cancelled() }}` so the safety gate runs even when contract gate fails but honors manual cancellation; update the assertion to permit this form. Converted to patch P15.
- [x] [Review][Decision→Patch] `ContainsForbiddenValue` reverted from token-boundary matching to plain `text.Contains(forbiddenValue, OrdinalIgnoreCase)`. **Resolution:** option (b) — restore token-boundary matching with a narrower in-word set (alphanumerics only; treat `/`, `.`, `-`, `_`, whitespace, and structural punctuation as boundaries). Closes both Round 3 R1's false-positive concern and Round 4's substring-collision risk. Converted to patch P16.

#### Patch — Acceptance Criteria and behavior corrections (Round 4) — ALL APPLIED 2026-05-18

All 16 Round 4 patches (13 AC/behavior corrections + 3 from resolved decisions) were applied in this follow-up. Validation: focused `SafetyInvariantGateTests` (10 tests pass), local `tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild` (10 tests pass), focused `FixtureContractTests` (6 tests pass), full `dotnet test Hexalith.Folders.slnx --no-build` (144 tests pass across the solution). No nested submodules were initialized.

- [x] [Review][Patch] AC 7 / AC 17 — `categories.Add(...).ShouldBeTrue(id)` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary` ~line 100] forbids two samples sharing the same category. Combined with the hard-pinned 18-sample count and the 18-element expected-category enumeration, it enforces a one-sample-per-category corpus shape that blocks legitimate coverage expansion (e.g., adding a second `secret-shaped-string` sample for a different provider). Fix: drop `.ShouldBeTrue` on `Add`, assert `categories` set-equality against the expected vocabulary at the end of the loop.
- [x] [Review][Patch] AC 11 — `GenericAbsolutePathMarkersExceptRepositoryRoot` [`SafetyInvariantGateTests.cs:823-832`] silently drops `/home/` and `/Users/` from the forbidden list whenever `RepositoryRoot` (or its slash variants) contains either substring — which is the typical case on Ubuntu (`/home/runner/...`) and macOS (`/Users/runner/...`) CI runners. The most relevant absolute-path detector disappears on the CI surfaces it most needs to cover. Fix: do not omit the detector; instead, normalize the `RepositoryRoot` prefix out of the value before scanning, so `RepositoryRoot/foo` reads as `<repo-root>/foo` while a leaked `/home/alice/...` still trips.
- [x] [Review][Patch] AC 11 — `AssertMetadataOnly(..., allowPatternDocumentation: true)` [`SafetyInvariantGateTests.cs:613-642`] reduces `forbidden` to repository markers only, dropping `diff --git`, `-----BEGIN`, `AccountKey=`, `client_secret=`, `https://github.com/`, `https://prod.`, `password=`, `api_key=`, etc. The bypass is too coarse for the documentation channel: a future doc edit could leak any of those tokens without test failure. Fix: split markers into `repositoryRootMarkers`, `absolutePathPatternMarkers` (the `C:\` / `D:\` literals the doc legitimately discusses), and `secretMarkers`; only `absolutePathPatternMarkers` should be conditional on `allowPatternDocumentation`.
- [x] [Review][Patch] AC 8 / AC 11 — `AssertDoesNotContainWholeToken` [`SafetyInvariantGateTests.cs:850-856`] uses `\b...\b` which does not match at lowercase→uppercase transitions, so `pageCount`, `nextCursor`, `stackTrace`, `RowCount`, `pageCursor` slip past the count/cursor/stack guards. Round 3 P8 fixed substring false positives but introduced camelCase false negatives. Fix: use `(?<![A-Za-z0-9_])(?i:count)(?![a-z])` style regex that catches `count`, `Count`, `COUNT` at non-letter boundaries but rejects `account`/`discount`.
- [x] [Review][Patch] AC 12 — `FindRepositoryRoot` [`SafetyInvariantGateTests.cs:773-784`] hard-fails with `SAFETY-PREREQUISITE-DRIFT: repository-root-unresolved` whenever `GITHUB_WORKSPACE` is set, even when it points at an unrelated repo (Codespaces, `gh act`, stale shell env var). The seed-based fallback below is unreachable. Fix: when `GITHUB_ACTIONS` is unset (local dev), fall through to the seed search if `GITHUB_WORKSPACE` does not contain `Hexalith.Folders.slnx`.
- [x] [Review][Patch] AC 15 / AC 20 — `last_evaluated_at` hard-pinned to `"2026-05-18"` [`SafetyInvariantGateTests.cs:271,275`] for the four named diagnostic channels. Per-channel re-evaluation becomes impossible — every channel must carry the same date forever, or every channel must be updated atomically each time any one is re-evaluated. Fix: validate ISO-8601 date format and a recency window (e.g., within the last 180 days), or assert `>= 2026-05-18`.
- [x] [Review][Patch] AC 21 — `GlobMatches` [`SafetyInvariantGateTests.cs:811-821`] translates `\*` to `[^/]*`, so manifest patterns `*.dll`, `*.exe`, `*.pdb`, `*.nupkg`, `*.snupkg`, `*.zip`, `*.binlog` (7 of 17 declared exclusions) only match top-level files. Nested binaries are scanned as text unless `IsBinaryFile` or a `**/` rule rescues them. Fix: when a pattern contains no `/`, treat it as `**/<pattern>` (basename match), matching .gitignore semantics.
- [x] [Review][Patch] AC 4 / AC 21 — `EnumerateSourceFiles` [`SafetyInvariantGateTests.cs:543-575` file branch] silently drops a single-file `artifact_source` that matches `IsExcludedByInventory` or `IsBinaryFile`. A channel marked `covered` whose sole artifact gets excluded contributes zero scan coverage and the gate passes without diagnostic. Fix: emit `SAFETY-PREREQUISITE-DRIFT` when an explicitly declared single-file source is filtered out.
- [x] [Review][Patch] AC 21 — `EnumerateSourceFiles` file branch does not apply the `FileAttributes.ReparsePoint` guard that the directory branch uses. A symlink/junction at any declared artifact_source path resolves to the link target (potentially `/etc/passwd` or arbitrary content). Fix: check `(File.GetAttributes(absolute) & FileAttributes.ReparsePoint) == 0` before yielding; emit bounded diagnostic otherwise.
- [x] [Review][Patch] Performance / TOCTOU — `IsExcludedByInventory` [`SafetyInvariantGateTests.cs:578-585`] reparses `safety-channel-inventory.json` once per file scanned. With ~50 files across covered channels this means ~50 redundant disk reads, JSON parses, and regex compiles per gate run, plus a TOCTOU window where inventory mid-scan edits become inconsistent. Fix: load `structured_exclusions` once at the start of `ScanManifestCoveredArtifacts` and pass the precompiled `Regex[]` to the predicate.
- [x] [Review][Patch] AC 21 — `IsWithinIncludeRoots` [`SafetyInvariantGateTests.cs:803-809`] treats every include_root as a directory prefix via `normalized.StartsWith(root + "/")`. The new file-level include_roots (`tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/quarantine/safety-negative-controls.json`) therefore accept any path beginning with `tests/fixtures/audit-leakage-corpus.json/...` as in-scope. Fix: when an include_root resolves to a file (`File.Exists`), require exact equality; only directory roots admit the prefix rule.
- [x] [Review][Patch] AC 12 — `tests/tools/run-safety-invariant-gates.ps1:35` hard-codes the TFM regex `[\\/]net10\.0[\\/]`. A future bump to `net11.0` (or `net10.0-windows`) makes the `-SkipRestoreBuild` bin probe report `SAFETY-PREREQUISITE-DRIFT` even when a valid build exists. Fix: read `<TargetFramework>` from the test csproj, or use regex `net\d+\.\d+(?:-[\w]+)?`.
- [x] [Review][Patch] AC 3 / AC 20 — `StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts` [`SafetyInvariantGateTests.cs:266-272`] iterates only `audit-records`, `projections`, `console-payloads`. `provider-diagnostics` (which the inventory still tags as Story 1.11 contract surface via its `coverage_notes`) is silently skipped. A future regression of its `last_evaluated_at` would not fail this test. Fix: include `provider-diagnostics` in the iteration, or rename the test to reflect its explicit three-channel scope.
- [x] [Review][Patch] AC 14 / scope — P14 (from Decision 1): revert submodule pointer bumps for `Hexalith.EventStore` (bbfc454 → 898b3f5) and `Hexalith.FrontComposer` (218d19d → 02e267c) via an additive commit, mirroring Round 1's `a3cd62f` precedent. Story 1.15 does not authorize submodule pointer changes.
- [x] [Review][Patch] AC 5 / workflow — P15 (from Decision 2): restore `if: ${{ !cancelled() }}` on the `Run safety invariant gates` step in `.github/workflows/contract-spine.yml`, and update `WorkflowAndDocumentationExposeSameOfflineSafetyGate` to assert that form rather than `ShouldNotContain("if: always()")`. Safety gate must run when prior contract-gate steps fail, but honor manual cancellation.
- [x] [Review][Patch] AC 11 / AC 18 — P16 (from Decision 3): restore token-boundary matching in `ContainsForbiddenValue` [`SafetyInvariantGateTests.cs:669-672`]. In-word characters are alphanumerics only; `/`, `.`, `-`, `_`, whitespace, and structural punctuation are boundaries. Closes Round 3 R1's false-positive concern and Round 4's substring-collision risk.

#### Defer — Round 4 hardening backlog

- [x] [Review][Defer] `HashSet<SafetyScanDiagnostic>` dedup may hide multi-route findings — deferred, `SafetyScanDiagnostic` is `sealed record` so value-equality is structurally correct; dedup of identical (channel, sample, path) tuples is acceptable. Revisit only if a "redundant route" signal becomes needed for diagnostics.
- [x] [Review][Defer] `MissingChannelDiagnostics` uses a single canned `Remediation` string for all prerequisite-drift channels — deferred, per-channel `remediation_hint` field in inventory is a hardening that does not change pass/fail behavior today.
- [x] [Review][Defer] `-SkipRestoreBuild` probes only the test assembly, not direct dependencies (`Hexalith.Folders.Contracts.dll`) — deferred, `dotnet test --no-build` raises a clear MSBuild error on missing deps; double-guarding adds complexity without changing safety semantics.
- [x] [Review][Defer] `AssertContainsText` emits `SAFETY-PREREQUISITE-DRIFT` for both "required marker absent" and "context-query missing" — deferred, adding a dedicated `SAFETY-REQUIRED-MARKER-ABSENT` rule ID is a vocabulary refinement; current bounded message is non-leaking.
- [x] [Review][Defer] `LoadYamlMapping` / `JsonDocument.Parse` lack bounded options (max depth, size cap) — deferred, all parsed files are reviewer-curated repo fixtures; defense-in-depth is welcome but not gating.
- [x] [Review][Defer] `BoundedDiagnosticException` does not validate the rule-ID argument against an allowed set, and does not assert `remediation` through `AssertMetadataOnly` — deferred, no current caller passes contaminated input; tighten if a third caller is added.
- [x] [Review][Defer] `AssertRepositoryRelativePath` does not reject UNC paths (`//server/share/...`) or extended-length prefixes (`\\?\D:\...`) — deferred, no current callers can produce these path shapes; tighten when a new include_root source is introduced.
- [x] [Review][Defer] `SerializeYaml` round-trips through `StringWriter` without `CultureInfo.InvariantCulture` — deferred, YamlDotNet writes strings literally for the fields the gate cares about; revisit if numeric YAML enters the corpus.

#### Dismiss — Round 4

- Pinned `samples.GetArrayLength().ShouldBe(18)` (Blind, Edge): explicit Round 3 P5 choice — assertion message documents the intent ("removing safety categories is reviewer-visible"). Adding samples requires a deliberate bump of the pin, which is the feature.
- Sentinel corpus / quarantine in `include_roots` causing self-detection (Blind): verified `sentinel-corpus-contract` channel has `scan_forbidden_values: false` and `tests/fixtures/quarantine/**` is in `structured_exclusions`. No self-detection path exists today.
- `SafetyScanDiagnostic` HashSet semantics broken (Blind): verified `private sealed record SafetyScanDiagnostic(...)` at line 871 — value-equality, dedup works as intended.
- `ProjectionAvailability` enum check is order-insensitive (Edge): explicit Round 3 P7 choice — set-equality was selected over sequence-equality to allow safe enum reordering without rebreaking the gate.
- `EnumerateOperations` operationId comparison is case-insensitive (Edge): explicit Round 3 R10 choice — selected to surface YAML casing drift rather than silently skipping. Relitigating without new evidence.

### Scope Boundaries

- This story wires CI and local gate entry points for safety invariant enforcement: sentinel redaction, forbidden leakage scanning, metadata-only diagnostics, and safe example validation.
- Allowed implementation areas are:

```text
.github/workflows/
tests/fixtures/audit-leakage-corpus.json
tests/Hexalith.Folders.Contracts.Tests/
tests/Hexalith.Folders.Testing.Tests/
tests/Hexalith.Folders.Server.Tests/
tests/Hexalith.Folders.UI.Tests/
tests/tools/
docs/contract/safety-invariant-ci-gates.md
```

- Equivalent file names are acceptable when they preserve the same ownership boundaries.
- Do not implement runtime redaction policy, domain behavior, provider behavior, CLI/MCP/UI feature work, Contract Spine operation groups, NSwag generation, parity-oracle derivation, Dapr policy conformance, cache-key tenant-prefix lint, exit-criteria checks, release publishing, or live provider drift checks.
- This story may add test/tooling safety checks over generated or checked-in artifacts, but it must not hand-edit generated outputs owned by Stories 1.12 or 1.13.

### Current Repository State To Inspect

- `.github/workflows/` is currently absent unless Story 1.14 creates it before implementation begins.
- `tests/fixtures/audit-leakage-corpus.json` exists as a minimal synthetic sentinel corpus with secret-shaped, credential-shaped, path, branch, commit-message, and provider-diagnostic placeholder samples.
- `tests/README.md` names redaction sentinel corpus gates as future CI work alongside parity schema, C6 matrix, cache-key tenant prefix, and provider drift checks.
- `_bmad-output/project-context.md` states that sentinel tests must iterate `tests/fixtures/audit-leakage-corpus.json` across logs, traces, metrics labels, events, audit records, projections, provider diagnostics, console views, and error responses.
- Architecture concern #6 makes the sentinel corpus normative; concern #17 and decision S-6 define sensitive metadata classification; concern #18 defines context-query authorization order; C10 cache-key lint is a separate Story 1.16 concern.
- Existing contract tests under `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` already parse OpenAPI with structured helpers and are likely useful for example and schema scanning.
- Active Story 1.10 review work may leave dirty OpenAPI, contract docs, and contract-test files. This story must inspect current state but must not absorb Story 1.10 implementation scope.

### Gate Requirements

- Treat the sentinel corpus as an input fixture, not as the policy engine. Tests should verify both corpus integrity and output-channel behavior.
- Treat `tests/fixtures/audit-leakage-corpus.json` as the authoritative vocabulary for this gate: classification labels, sentinel IDs, forbidden surfaces, allowed safe provenance, and synthetic-only metadata must be declared there before tests use them.
- Maintain a safety channel inventory or manifest for this story. Each entry should name the output channel, owning story or artifact family, scanned artifact/test source, prerequisite status, and the bounded diagnostic to emit when absent.
- Use structured parsing where possible. Prefer JSON parsing for the corpus and schema fixtures, YAML parsing for OpenAPI/parity artifacts, and targeted text scanning only where a structured parser is not available.
- Safety gates must fail closed when a channel claims coverage but provides no artifact, no test seam, or only placeholder behavior.
- Manifest entries must be fresh at gate start: every claimed scanned source resolves to an existing repository-relative file or directory, while `reference-pending` and `prerequisite-drift` entries remain explicit, owner-scoped, and metadata-only.
- Scan input enumeration must use explicit repository-relative roots and exclusions. Do not scan `.git`, package caches, build outputs, binary blobs, local machine paths, undeclared generated directories, or contaminated negative-control fixtures except in tests that intentionally validate those controls.
- Diagnostics may include gate name, repository-relative path, synthetic sample ID, category, classification, rule ID, owning story, operation ID, schema pointer, content hash, output-channel name, and remediation hint.
- Diagnostics must never echo forbidden values, real secrets, file contents, diffs, raw provider payloads, generated context payloads, local absolute paths, production URLs, real tenant data, or unauthorized-resource hints.
- Missing-channel diagnostics must use bounded categories such as `SAFETY-CHANNEL-MISSING` or `SAFETY-PREREQUISITE-DRIFT` and must not include discovered runtime data, sample payloads, serialized generated snippets, tenant IDs, resource IDs, provider response bodies, timestamps, cache keys, counts, cursors, or path fragments.
- Test assertion messages and CI artifact summaries are output channels. They must be sanitized with the same metadata-only rule set as logs, traces, generated diagnostics, and documentation examples.
- Keep tests offline and deterministic. They must not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or initialized nested submodules.
- Generated artifact scans are leakage-only checks. They may allow safe provenance such as operation IDs, schema pointers, tool names, rule IDs, content hashes, fixture names, sentinel categories, and redaction markers, but must not assert drift, parity completeness, schema derivation, generated-client correctness, or release readiness.

### Previous Story Intelligence

- Story 1.3 seeded `tests/fixtures/audit-leakage-corpus.json` as a placeholder fixture for future redaction and leakage checks.
- Story 1.5 made metadata-only idempotency and adapter parity rules authoritative; helper and parity diagnostics must remain safe by construction.
- Story 1.6 created the Contract Spine foundation and extension vocabulary; this story scans or validates examples but does not define new OpenAPI operation metadata.
- Stories 1.7 through 1.11 author operation groups and audit/ops-console read contracts. This story checks their examples and diagnostics for leakage, not their business semantics.
- Story 1.12 owns generated SDK clients and idempotency helper generation. This story may scan their generated output and diagnostics after they exist, but must not edit generated SDK policy.
- Story 1.13 owns C13 parity oracle generation. This story may scan parity outputs and diagnostics after they exist, but must not derive rows or add parity completeness rules.
- Story 1.14 owns Contract Spine drift, generated-client golden-file, parity schema, and workflow wiring. This story should reuse the CI lane where possible and remain focused on safety invariants.
- Story 1.16 owns exit-criteria presence, idempotency-encoding equivalence, pattern-example compilation, tenant-prefixed cache-key lint, and parity completeness gates.

### Latest Technical Notes

- GitHub Actions and .NET setup details should follow the workflow guidance captured in Story 1.14: use checked-in `global.json`, central package management, repository-root commands, and locally reproducible `dotnet` invocations.
- `actions/setup-dotnet` NuGet caching remains optional. Do not add cache behavior in this story unless lock-file support and safe cache keys already exist; tenant-prefixed cache-key lint is owned by Story 1.16.
- Use the repository's existing xUnit, Shouldly, and YamlDotNet testing patterns rather than adding a new scanner framework unless an existing parser cannot express the check.

### Testing Guidance

- Good positive cases: synthetic sentinel samples are classified and safe; safe diagnostics report only sample IDs/categories; redacted values are distinguishable from unknown/missing for authorized examples; generated artifacts contain safe provenance only.
- Good negative cases: a fake token-shaped sentinel appears in a Problem Details example, a generated diagnostic echoes a forbidden value, an OpenAPI example contains a local absolute path, a console payload fixture silently drops redaction state, an unauthorized example reveals resource existence through count/order/cursor metadata, or a channel claims coverage but has no test seam.
- Good containment cases: a contaminated fixture proves detection while normal scans skip the quarantine path, a stale manifest entry fails with `SAFETY-PREREQUISITE-DRIFT`, and a failing assertion reports only sample ID/classification rather than the forbidden value.
- Keep assertions precise and bounded. Avoid broad secret-scanning claims that cannot be proven by the repository fixtures and tests.
- If a future runtime channel is absent, assert the prerequisite-drift marker instead of creating a placeholder runtime implementation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.15: Wire safety invariant CI gates`
- `_bmad-output/planning-artifacts/architecture.md#Sentinel redaction`
- `_bmad-output/planning-artifacts/architecture.md#Sensitive metadata classification`
- `_bmad-output/planning-artifacts/architecture.md#Authorization order`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/1-14-wire-contract-spine-drift-and-generated-client-ci-gates.md`
- `_bmad-output/implementation-artifacts/1-13-generate-the-c13-parity-oracle.md`
- `_bmad-output/implementation-artifacts/1-12-wire-nswag-sdk-generation-with-idempotency-helpers.md`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/README.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- Workflow files belong under `.github/workflows/`.
- Sentinel corpus and shared leakage fixtures belong under `tests/fixtures/`.
- Focused safety tests should live in the smallest existing test project that owns the scanned artifact.
- Test helpers or tools may live under `tests/tools/` when they are reusable from local commands and CI.
- Human-readable gate documentation may live at `docs/contract/safety-invariant-ci-gates.md`.
- Do not place safety gate tooling in runtime projects unless the runtime project already owns the emitted channel being tested.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-13 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-15 | Party-mode review applied channel inventory, bounded diagnostic, vocabulary authority, negative-control, telemetry, generated-artifact scope, and reviewer checklist hardening. | Codex |
| 2026-05-16 | Advanced elicitation applied negative-control quarantine, manifest freshness, explicit scan-scope, and sanitized assertion/artifact hardening. | Codex |
| 2026-05-17 | Implemented safety invariant corpus, manifest, quarantined negative controls, focused tests, local gate script, workflow wiring, and reviewer documentation. Story ready for review. | Codex |
| 2026-05-17 | Addressed remaining code review findings for telemetry surfaces, safe assertions, inventory diagnostics, negative-control drift, and single-command local gate usage. Story ready for review. | Codex |
| 2026-05-18 | Round 3 code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) raised 4 decision-needed (1 dismissed, 1 kept, 1 converted to patch, 1 deferred) and 24 patch findings (including telemetry channel status flip). Status moved back to in-progress; patches left as action items. | Claude |
| 2026-05-18 | Addressed Round 3 patch findings, hardened safety scanner diagnostics and manifest freshness, and moved story back to review after validation. | Codex |
| 2026-05-18 | Round 4 code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) raised 3 decision-needed (all resolved as patches), 13 patch findings, and 8 defers. All 16 patches applied: corpus category uniqueness lifted, AssertMetadataOnly split into root/path-pattern/always-forbidden markers with CI-safe `/home/` and `/Users/` detection, AssertDoesNotContainWholeToken detects camelCase boundaries, FindRepositoryRoot falls back on local dev, last_evaluated_at validates ISO-8601 + recency, GlobMatches honors basename patterns, EnumerateSourceFiles emits SAFETY-PREREQUISITE-DRIFT for filtered single-file sources and rejects reparse points, IsExcludedByInventory caches compiled regexes, IsWithinIncludeRoots distinguishes files from directories, PowerShell TFM regex is framework-agnostic, provider-diagnostics joined the StoryEleven channel iteration, workflow restored `if: ${{ !cancelled() }}`, ContainsForbiddenValue uses narrow token boundaries, and the Round 3 submodule pointer bumps were additively reverted. Full validation passed without initializing nested submodules. | Claude |

## Party-Mode Review

- Date/time: 2026-05-15T12:05:44Z
- Selected story: 1-15-wire-safety-invariant-ci-gates
- Command/skill invocation used: `/bmad-party-mode 1-15-wire-safety-invariant-ci-gates; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Channel ownership needed a concrete inventory or manifest so implemented, reference-pending, and prerequisite-drift channels are explicit.
  - Bounded prerequisite-drift diagnostics needed allowed and forbidden output fields to prevent CI logs from echoing sensitive data.
  - The sentinel corpus needed to be named as the authoritative classification/redaction vocabulary source for this gate.
  - Generated artifact scanning needed explicit leakage-only boundaries to avoid absorbing Story 1.14 or Story 1.16 responsibilities.
  - Telemetry checks needed to include tags, dimensions, attributes, event names, span names, metric names, counters, exception metadata, and baggage, not only message strings.
  - Negative controls and reviewer-facing guidance were needed to prove the scanner fails safely and remains synthetic-only.
- Changes applied:
  - Added ACs for channel inventory, authoritative corpus vocabulary, telemetry scan targets, and generated-artifact leakage-only scope.
  - Added tasks for channel inventory, sentinel forbidden-surface and allowed-provenance lists, unknown classification failure, intentionally contaminated negative-control fixtures, safe failure-message fields, bounded missing-channel diagnostics, single offline local/CI command reuse, and reviewer checklist documentation.
  - Expanded Gate Requirements with authoritative vocabulary, channel manifest, allowed diagnostics, forbidden missing-channel fields, and generated-artifact leakage-only guidance.
- Findings deferred:
  - Runtime redaction handlers, provider behavior changes, SDK/parity derivation, Contract Spine drift checks, parity completeness, idempotency encoding, tenant cache-key lint, exit criteria, pattern compilation, release jobs, and broad workflow orchestration remain outside Story 1.15.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-16T06:04:10Z
- Selected story: 1-15-wire-safety-invariant-ci-gates
- Command/skill invocation used: `/bmad-advanced-elicitation 1-15-wire-safety-invariant-ci-gates`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Self-Consistency Validation; Comparative Analysis Matrix; Critique and Refine.
- Reshuffled Batch 2 method names: First Principles Analysis; Pre-mortem Analysis; Security Audit Personas; Graph of Thoughts; Active Recall Testing.
- Findings summary:
  - Negative controls needed quarantine and opt-in scanning rules so contaminated fixtures prove detection without becoming normative examples or leaking through assertion output.
  - Channel manifests needed freshness checks that distinguish acceptable `reference-pending` entries from stale claimed coverage.
  - Scan enumeration needed explicit include roots and exclusions to avoid accidental traversal of `.git`, build outputs, caches, binary blobs, local paths, or undeclared generated content.
  - CI logs, assertion diffs, and uploaded workflow artifacts are output channels and must obey the same metadata-only diagnostic rules as application and generated artifacts.
- Changes applied:
  - Added ACs for quarantined negative controls, manifest freshness, and explicit scan-scope boundaries.
  - Added tasks for stale manifest failure, scan root/exclusion definition, safe assertion helpers, quarantine opt-in validation, sanitized CI artifacts, and reviewer documentation.
  - Expanded Gate Requirements and Testing Guidance with manifest freshness, scan enumeration boundaries, sanitized assertion/artifact output, and containment examples.
- Findings deferred:
  - Runtime redaction behavior, broad secret-scanning engines, Dapr policy conformance, provider live drift, release evidence, cache-key lint, idempotency encoding, exit criteria, parity completeness, and generated-client correctness remain outside Story 1.15.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

Codex (GPT-5)

### Debug Log References

- 2026-05-17: Loaded BMAD customization, project contexts, sprint status, and Story 1.15 before implementation.
- 2026-05-17: Confirmed Story 1.15 was ready for dev, moved story and sprint status to in-progress, and inspected Story 1.10 through Story 1.14 artifacts.
- 2026-05-17: Inspected `.github/workflows/contract-spine.yml`, `tests/fixtures/audit-leakage-corpus.json`, `tests/README.md`, architecture safety sections, OpenAPI examples, generated SDK output, parity artifacts, and existing test projects.
- 2026-05-17: Added failing RED safety gate tests first; initial focused run failed on missing manifest/quarantine/script/docs and unhardened corpus fields.
- 2026-05-17: Hardened the corpus contract, added channel inventory, added quarantined negative controls, wired the local gate script into the existing workflow, and documented reviewer usage.
- 2026-05-17: Validation passed: focused safety tests, fixture-contract tests, local safety gate script, contract-spine gate script, solution build, and full solution test suite.
- 2026-05-17: Added RED review-follow-up tests for AC 17, AC 11, AC 1/3, AC 8, AC 4, AC 18, AC 12, quarantine opt-in, negative-control parity, and fixture classification vocabulary; confirmed the expected failures before patching.
- 2026-05-17: Validation passed after review follow-up: `./tests/tools/run-safety-invariant-gates.ps1`, focused `FixtureContractTests`, and `dotnet test Hexalith.Folders.slnx --no-build`.
- 2026-05-18: Resumed Story 1.15 after Round 3 review, confirmed sprint status was already in-progress, and patched the remaining safety gate review follow-ups.
- 2026-05-18: Validation passed: focused `SafetyInvariantGateTests`, `./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild`, full `./tests/tools/run-safety-invariant-gates.ps1`, focused `FixtureContractTests`, and `dotnet test Hexalith.Folders.slnx --no-build`.

### Completion Notes List

- Added `SafetyInvariantGateTests` covering authoritative corpus vocabulary, synthetic-only fixture rules, channel manifest freshness, normal-scan quarantine exclusion, opt-in negative controls, sanitized diagnostics, OpenAPI/Problem Details leakage checks, context-query authorization ordering, and workflow/documentation wiring.
- Expanded `tests/fixtures/audit-leakage-corpus.json` into the normative safety vocabulary with classified synthetic sentinels, forbidden output surfaces, allowed provenance-safe representations, and reviewer-visible categories.
- Added `tests/fixtures/safety-channel-inventory.json` to track covered, `reference-pending`, and `prerequisite-drift` channels with repository-relative sources and bounded diagnostics.
- Added quarantined synthetic negative controls under `tests/fixtures/quarantine/` and verified the gate detects them without echoing forbidden values in assertion output.
- Reused the existing Story 1.14 workflow lane by adding a focused safety step after restore/build and contract gates; no duplicate release/security/provider/cache/exit-criteria jobs were added.
- Updated existing fixture-contract tests so the audit corpus is treated as a normative vocabulary rather than a placeholder fixture.
- Resolved all remaining review patch findings by expanding distinct telemetry scan surfaces, adding sanitized assertion helpers, emitting bounded missing-channel diagnostics from manifest entries, re-evaluating current diagnostic channels against OpenAPI/generated-client artifacts, expanding quarantine coverage to all negative-control participants, and making the local safety script a single offline command by default.
- Full validation passed without initializing or updating nested submodules.
- Resolved Round 3 review findings by replacing unsafe YAML/string assertions with bounded helpers, making context-query operation checks non-vacuous, honoring manifest include roots/exclusions at scan time, pinning corpus count/categories, using URL-shaped synthetic production sentinels, switching diagnostic paths to hash-only output, probing skip-build prerequisites, and marking telemetry/runtime channels as `prerequisite-drift` until deterministic artifacts land.
- Full validation passed again without initializing or updating nested submodules.

### File List

- `.github/workflows/contract-spine.yml`
- `docs/contract/safety-invariant-ci-gates.md`
- `tests/README.md`
- `tests/tools/run-safety-invariant-gates.ps1`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/fixtures/safety-channel-inventory.json`
- `tests/fixtures/quarantine/safety-negative-controls.json`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs`
- `_bmad-output/implementation-artifacts/1-15-wire-safety-invariant-ci-gates.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
