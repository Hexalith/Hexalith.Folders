# Scoped A6b pre-approval review — 1.17-GENERATE delta `292ea32..dc8a4e6`

- Date: 2026-09-22
- Authorized by: Jerome ("do recommended"), one pass, report-only, beyond the review-loop ceiling; `review_loop_iteration` not incremented; spec, governance and sprint files not edited by this review.
- Scope: 68 story-owned files (`scripts/pd10-v2-story-owned-paths.txt`), +3219/−1021. Group A (64 hand-written/generator/test files) reviewed by Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; Group B (generated `hexalith.folders.v2.yaml`, `HexalithFoldersClient.g.cs`, `HexalithFoldersIdempotencyHelpers.g.cs`) by a focused generated-artifact reviewer.
- Review target digests: conformance set `249570480be2a7503ad79d33c0b026fd913c8f05f4dfc92cb2b054b1d08b0195`, candidate set `76e36d37bfcfc97e575bf6847f8cc17c0691841c7f244b75e711220abf41a0be`, 196 artifacts.
- Stop rule: only HIGH-severity, reachable, in-delta defects block A6b re-approval; everything else defers.

**Result: 1 blocking (high, patch), 17 deferred, 10 rejected.**

**Blocking finding fixed 2026-09-22 (Jerome chose option 1: fix and reseal):**
- `scripts/generate-pd10-v2-contract.py` declares the `ValidateProviderReadiness` tuple `429|provider_rate_limited|provider_rate_limited|true|retry|visibility`, and a new `validate_declared_statuses_have_runtime_tuples` guard fails generation when any declared error status lacks an operation-bound runtime tuple. The guard was confirmed to reject the pre-fix generator.
- Regenerated: `hexalith.folders.v2.yaml`, `Pd10V2RuntimeResponseCatalog.g.cs`, `HexalithFoldersIdempotencyHelpers.g.cs` (NSwag client byte-identical), `tests/fixtures/parity-contract.yaml`, and the `tests/fixtures/previous-spine.yaml` baseline digest via `--initialize-baseline` (the documented mutation rule after an intentional contract change).
- Regression: `GoldenLifecycleParityTests.CandidateProviderReadinessRateLimitKeepsItsDeclaredTuple` drives a historical 429 through the real seam and generated SDK; it fails with 503 against the pre-fix catalog and passes with the fix.
- Resealed: conformance `4c8f33f0cb00afac3a5541eab7b9d30ec15b832d6114316ff672338cc02f5be7`, candidate set `c5bea4292e918605ba9a4331012806fc0e3e3da818dea66ae68c080929bf2c7f`, 196 artifacts.

## Blocking

- [x] [Review][Patch] **`ValidateProviderReadiness` 429 is declared but has no runtime tuple; a real provider rate limit is rewritten to the canonical 503** [scripts/generate-pd10-v2-contract.py:463; src/Hexalith.Folders.Server/Pd10V2RuntimeResponseCatalog.g.cs:689] — BH16-02 claimed fixed but is fixed only for the 422 half. The contract declares `'429': ProviderRateLimited` (no examples); the catalog allows status 429 but lists no 429 tuple; `ProviderReadinessEndpoints.cs:406-414` emits `429|provider_rate_limited` (with retry-after), so `IsDeclaredProblem` fails and the seam writes `AuthorityUnavailable` 503 (`Pd10V2CandidateCompatibilitySeam.cs:750-757`). Root cause: `declared_runtime_problems()` derives tuples only from examples. Violates the frozen AC "the generated contract admits every reachable response and no unreachable … tuple". Fails closed (both retryable), but a rate-limited provider is reported as an authority outage and the retry-after hint is lost. Fix: derive/declare the 429 tuple (including its exact detail keys) for this operation, regenerate contract/catalog/SDK, add a seam test with a downstream 429, reseal.

## Deferred (real, non-blocking under the stop rule)

- [x] [Review][Defer] BH16-12 remainder: `EffectivePermissions.permissions` and `CommitEvidence.auditMetadataKeys` still `Required.DisallowNull`+`Ignore`; omission deserializes to an empty list [GeneratedClientPostProcessor.cs `RequireJsonProperties`] — exact-attribute match silently skips attributes carrying `ItemConverterType`. Fails toward the restrictive side. (Path/kind/pathPolicyClass claims verified FALSE: enforced by `Oq2WireObjectConverter`.)
- [x] [Review][Defer] BH16-13 half-closed: `JsonExtensionData` removed but no `MissingMemberHandling.Error`/raw-shape check, so unknown members on `additionalProperties: false` success bodies are silently ignored.
- [x] [Review][Defer] BH16-05 order dependence: `CanonicalAccessState` returns `negativeStates[0]`; `[revoked, stale]` → 404, `[stale, revoked]` → 503 [Pd10V2CandidateCompatibilitySeam.cs:1545].
- [x] [Review][Defer] BH16-08 narrower than spec: final reauthorization in `FolderCreationService`, `RepositoryBackedFolderCreationService`, `ConfigureProviderBindingService` uses `includeFolderAcl: false` (drops ACL layer and `OperationScope`) although the initial `AuthorizeAsync` included them; an ACL revocation between the two checks is not detected.
- [x] [Review][Defer] BH16-07: folderless `StrictRead` operations (`GetProviderBinding`, `ValidateProviderReadiness`, `GetProviderSupportEvidence`) run validator/Dapr layers with `LayeredFolderOperationPolicy.Mutation()`.
- [x] [Review][Defer] BH16-15: the conformance inventory is a hand-kept allowlist and `Pd10ConformanceSetTests` reads the same file (circular); gitlink binding dropped. Verified no omission today: only `release.yml`, `Directory.Packages.props`, `c3-retention.md`, `ReleasePackageConformanceTests.cs` and root gitlinks are excluded (all recorded in the register's A6b reopen note).
- [x] [Review][Defer] Generator overwrites example `status` to the response key and feeds 422/423/428/429 examples into the runtime catalog instead of failing on mismatch [generate-pd10-v2-contract.py:468-480, 532]; hides drift (e.g. 428 `authorization_revocation_detected` has only the seam remap as producer).
- [x] [Review][Defer] Missing regressions for claimed fixes (pre-verified): operation-mismatched but in-vocabulary freshness in CLI/MCP; 409→423/428 file-mutation remap; per-service final-reauth revocation race; case-variant duplicate JSON; parity-generator "absent from historical v1" branch; EC16-05 baseline-init validation; MCP `lock_conflict` projection test replaced.
- [x] [Review][Defer] Typed-problem projection fails closed when the SDK `BaseUrl` carries a path prefix (route matching on absolute path + in-delta `operationId is null → false`) [HexalithFoldersOperationContext.cs; Oq2ProblemProjection.cs].
- [x] [Review][Defer] CLI `--freshness` and MCP descriptions still advertise three values although each operation accepts one; test-only `ParseFreshness(string?)` overload defaults to `ListAuditTrail`.
- [x] [Review][Defer] Event 1018 logs the full exception — deferred: maybe-false; spec KEEPs event-1018 logging; settle by checking whether authorization exceptions can carry tenant/principal values in messages.
- [x] [Review][Defer] `PrepareWorkspace` 422 example content copied from the lock scenario (cosmetic).
- [x] [Review][Defer] `CanonicalErrorCode` members renumbered by inserting `Unsupported_provider_capability` (unreleased candidate; note before release).
- [x] [Review][Defer] Archive final reauthorization passes `ActorSafeIdentifier` as `PrincipalId` — deferred: maybe-false; green archive suites imply equality; settle by asserting actor-safe id == principal id on the `/process` path.
- [x] [Review][Defer] PRE-EXISTING (outside delta, high): generated `ValidateProviderReadinessAsync` binds success to `ProviderReadinessConsumer`, but the server returns `audience: authorized_operator`, so every real 200 throws in the SDK — documented and pinned by `GoldenLifecycleParityTests` since 2026-06-23.
- [x] [Review][Defer] PRE-EXISTING: UI pages (`Workspace`, `Provider`, `AuditTrail`, `OperationTimeline`, `IncidentStream`) send `eventually_consistent` to operations the v2 seam restricts to another class (seam enforcement predates the delta; UI untouched).
- [x] [Review][Defer] PRE-EXISTING: repository-backed create/bind fallback `422 provider_readiness_failed` and `details.finalState` on `reconciliation_required`/`unknown_provider_outcome` are undeclared and become the canonical 503 (fail-closed).

## Rejected

- `false` — multi-positive access state resolved by `FirstOrDefault`: the positive state only drives delegation; grants come from layered evidence and the executor treats all positive states alike.
- `false` — archive reauth synthesizes `ClaimTransformEvidence.Allowed`: same-request token claims cannot change; fresh tenant-projection/ACL/Dapr layers are re-evaluated (same design as `ReauthorizeMutationAsync`).
- `false` — `OrdinalIgnoreCase` duplicate detection rejects case-variant keys: required by BH16-10 (historical binder is case-insensitive).
- `false` — `synthetic` substring rejection in `FolderCommandValidator`: intentional BH15-22/VG16-05 conservative rule.
- `false` — `PreauthorizedRequestContext`/`State` made internal plus `InternalsVisibleTo`: required by BH16-09.
- `low` — SDK freshness parameter accepts any enum value (server returns declared 400).
- `low` — generator/post-processor edge inputs (missing allowlist, bad `$ref`, name-prefix lookups) fail loudly.
- `low` — removed `carriesHistoricalFingerprints` branch (new unconditional fingerprint rule covers current operations).
- `low` — duplicated `stream` retry header value untested; `AsyncLocal` operation context set in test fixtures.
- `low` — sample-test stub returns contradictory workspace-status data for unmatched GETs.
