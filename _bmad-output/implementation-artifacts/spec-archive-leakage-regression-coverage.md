---
title: 'Archive leakage regression coverage'
type: 'bugfix'
created: '2026-08-29'
status: 'done'
baseline_revision: '34fed52b58c693122f3af3e7597cdabff2193c9d'
baseline_commit: '34fed52b58c693122f3af3e7597cdabff2193c9d'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
warnings: []
deferred:
  - summary: >-
      Corpus loading (`RepositoryRoot()` + parse of `tests/fixtures/audit-leakage-corpus.json`) is
      copy-pasted across at least nine test classes instead of one shared labelled reader.
    evidence: |-
      The same discovery-and-parse block appears in FolderArchiveMetadataLeakageTests,
      FolderWorkspaceFileMutationAggregateTests, FolderWorkspaceCommitAggregateTests,
      WorkspaceLifecycleProjectionDeterminismTests, FolderAuditObservationTests, AuditEndpointsTests,
      AuditEndpointsSentinelTests, WorkspaceStatusEndpointTests and MemoriesFolderSearchSourceTests.
      Each copy is free to drift in labelling and in blank-value filtering, so "every sentinel is
      covered" cannot be checked centrally. Pre-existing; this story adds another consumer.
    location: >-
      tests/ (nine call sites); candidate home: src/Hexalith.Folders.Testing or tests/shared
    severity: medium
  - summary: >-
      The archive surface map uses an invented channel vocabulary that never meets the declared
      channel inventory, so archive-path channel coverage cannot be measured by any gate.
    evidence: |-
      FolderArchiveMetadataLeakageTests keys its surfaces `event`, `audit-record`, `projection`,
      `problem-details`, `log-template`, `trace-tags`, `generated-client-exception`, while
      tests/fixtures/audit-leakage-corpus.json (`forbidden_output_surfaces`) and
      tests/fixtures/safety-channel-inventory.json (`channels`) declare `events`, `audit-records`,
      `projections`, `problem-details-examples`, `logs`, `traces`, `generated-sdk` and ~18 more.
      Only 8 of 25 declared channels are swept on the archive path and nothing pins the shortfall.
      Pre-existing shape of the original [Fact]; not introduced by this story.
    location: >-
      tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
    severity: medium
  - summary: >-
      The per-sample `forbidden_output_surfaces` scoping in the corpus is ignored; every sentinel is
      asserted against every surface, encoding a stricter contract than the fixture's own policy.
    evidence: |-
      `correlation-metadata` deliberately omits `events`/`projections` from its forbidden list (with
      the note that production policies decide where correlation may remain visible), and
      `safe-provenance-operation-id` forbids only `provider-diagnostics`. The tests also ignore
      `participates_in` and `classification`. The suite passes only because the archive surfaces are
      built from safe defaults. Pre-existing.
    location: >-
      tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
    severity: low
  - summary: >-
      `AcceptedArchiveEventShouldCarryOnlyMetadataEvidence` hand-joins 8 of the 10 `FolderArchived`
      members, so a newly added property escapes that assertion silently.
    evidence: |-
      The [Fact] builds its subject with an explicit '|'-join that omits `IdempotencyFingerprint`
      and `OccurredAt`, unlike the corpus theories which serialize the whole record. Pre-existing
      and untouched by this story.
    location: >-
      tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
    severity: low
  - summary: >-
      A trailing bare LF defeats every canonical-identifier gate in the repository, because .NET's
      `$` matches before a final newline unless `RegexOptions.Multiline` is set.
    evidence: |-
      Verified: `^[a-z0-9._-]+$` accepts "safe-identifier\n" (it rejects "safe-identifier\r\n" and
      "safe\nidentifier"). So `FolderCommandRejected.CanonicalIdentifierOrNull("safe-identifier\n")`
      returns the value with the newline intact, and `FolderCommandValidator.IsSafeEvidenceIdentifier`
      / `FolderStreamName.IsValidSegment` accept a trailing newline too. That is a live log-injection
      vector on exactly the surfaces these canonical gates exist to protect. Pre-existing and out of
      scope here: the Intent's Block-If forbids changing production canonicalization. Fix shape:
      anchor with `\A...\z` instead of `^...$`.
    location: >-
      src/Hexalith.Folders.Server/FolderCommandRejected.cs:30 and
      src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:952
    severity: high
  - summary: >-
      Sibling corpus sweeps still assert leakage with value-printing `ShouldNotContain`, the exact
      idiom these two files ban, and they are the sites that drive sentinels through production.
    evidence: |-
      `MemoriesFolderSearchSourceTests.ShouldDropEveryLeakageCorpusSnippetFromResults:84` and
      `WorkspaceStatusEndpointTests:271` use bare `ShouldNotContain(sentinel)`, which renders both the
      sentinel and the actual payload into the assertion-messages channel that audit-leakage-corpus.json
      declares forbidden. They also use a raw substring scan, so a future corpus sample containing any
      character `JavaScriptEncoder.Default` escapes would serialize as `\uXXXX` and read as clean.
      Today's corpus is entirely plain ASCII, so the gap is latent rather than live.
      `AuditEndpointsSentinelTests:76` already truncates for this reason; the repo is inconsistent.
    location: >-
      tests/Hexalith.Folders.Server.Tests/MemoriesFolderSearchSourceTests.cs:84
    severity: medium
  - summary: >-
      Two semantically incompatible leakage detectors now exist in the repository, and nothing pins
      which one is authoritative.
    evidence: |-
      `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` scans
      `OrdinalIgnoreCase` with a token-boundary rule, driven by each sample's declared
      `forbidden_output_surfaces` plus safety-channel-inventory.json. The detector added by this story
      is `Ordinal` with no token boundary but adds a JSON-decoded walk -- weaker against case mutation,
      stronger against `\uXXXX` escaping. Neither subsumes the other and no test records the divergence.
    location: >-
      tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
    severity: medium
  - summary: >-
      The accepted-archive-surface sweep is structurally incapable of failing; its 18 corpus rows scan
      constant payloads that no sentinel can ever reach.
    evidence: |-
      `AcceptedArchiveSurfaces()` builds all eight channel strings from `FolderCommandFactory.Archive()`
      safe defaults, and every corpus sentinel is rejected by `IsSafeEvidenceIdentifier`, so no corpus
      value can reach an accepted archive surface. Demonstrated: replacing all eight surface values with
      `string.Empty` still passes 18/18. Pre-existing (the original [Fact] had the same property) and
      intent-mandated -- the Intent's matrix row 3 and its "preserve existing accepted-surface checks"
      clause specify exactly this shape, so it was not in scope to change. Real coverage on this path
      would need a hostile-but-accepted caller value threaded into the archive command.
    location: >-
      tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
    severity: medium
  - summary: >-
      The entire `tests/Hexalith.Folders.Server.Tests` project runs in no CI gate lane, so this
      story's rejection-event regression trap gates nothing in CI.
    evidence: |-
      Verified: the project appears in no `$unitTestProjects` list across `tests/tools/*.ps1` and in
      no `.github/workflows/*.yml`; its only cross-project references are the `ScaffoldContractTests`
      assertions that it exists. 644 tests -- including the sibling corpus sweeps in
      `AuditEndpointsSentinelTests`, `WorkspaceStatusEndpointTests` and `MemoriesFolderSearchSourceTests`
      -- therefore gate nothing. Demonstrated: the mandated `SafeIdentifierRegex` widening turns 13
      rows red locally while CI stays green. Pre-existing and far wider than this story, which merely
      inherited the project as its home (`ScaffoldContractTests` forbids the alternative). Attempted in
      this pass and reverted: enrolment needs a four-file lockstep -- `run-baseline-ci-gates.ps1`,
      `BaselineCiWorkflowConformanceTests._baselineUnitProjects`, `docs/operations/baseline-ci-gates.md`
      and the generated `_bmad-output/gates/baseline-ci/latest.json` (pinned by
      `BaselineGateReportShouldStayMetadataOnlyWhenPresent`) -- and an honest regeneration of that report
      is blocked by the pre-existing format break below. Both the generated-artifact edit and the
      production edit are forbidden by this story's own AC8. The project is hermetic and fast
      (644/644 in ~17s, no sidecar, no browser, no network), so the baseline lane is its natural home.
    location: >-
      tests/tools/run-baseline-ci-gates.ps1 and tests/Hexalith.Folders.Server.Tests
    severity: high
  - summary: >-
      The blocking baseline CI lane is red on `main` today: `dotnet format whitespace --verify-no-changes`
      fails with 13 errors on one untouched production file.
    evidence: |-
      Reproduced on a clean tree at this story's baseline: `dotnet format whitespace
      Hexalith.Folders.slnx --verify-no-changes --no-restore --include ./src/ ./tests/ ./samples/`
      reports 13 `error WHITESPACE: Fix whitespace formatting. Insert '\s\s\s\s'` on
      src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs
      (lines 22-25 among others), a collection-expression indentation shape. The file is unmodified by
      this story and last changed by commit be36435 (2026-08-26, on main). The `format` gate runs before
      `unit-tests` and exits on failure, so every unit lane behind it is unreachable. Out of scope here:
      the fix edits a production source file, which this story's AC8 forbids.
    location: >-
      src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs:22
    severity: high
  - summary: >-
      Two of the three rejection events emitted by `/process` have zero test coverage anywhere, and the
      argument mapping that feeds all three is untested.
    evidence: |-
      `grep -rn "DuplicateWorkspaceLockRejected\|WorkspaceTransitionInvalidRejected" tests/` returns
      nothing, as does `grep -rn "IRejectionEvent" tests/`. Both records re-invoke
      `FolderCommandRejected.CanonicalIdentifierOrNull` / `NormalizeCommandTypeForRejection` inline
      (src/Hexalith.Folders.Server/DuplicateWorkspaceLockRejected.cs:22-46 and
      WorkspaceTransitionInvalidRejected.cs:22-46) and are emitted from
      FolderDomainProcessor.CreateRejectionEvent:1301,1325. Separately, that method sources its
      arguments from `result?.ActorPrincipalId`, `envelope.CorrelationId`, `envelope.MessageId` and
      `TryReadCanonicalExtension(...)`, none of which any test drives -- so a regression that echoed an
      envelope-supplied raw actor instead of the aggregate-nulled one leaves every new row green.
      Pre-existing; this story's rows call the factory directly, as the Intent's matrix specifies.
    location: >-
      src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1292-1330
    severity: medium
  - summary: >-
      The leakage detector and its fixture readers are now duplicated verbatim across the story's two
      test files, so a hardening applied to one copy silently does not apply to the other.
    evidence: |-
      `ContainsSentinel`, `JsonElementContainsSentinel`, `EscapeEveryCharacter`, `RequireWellFormedJson`,
      `SentinelById`, both fixture loaders, `RepositoryRoot` and both records (~150 lines) exist twice,
      as do three detector self-tests. Introduced by this story rather than pre-existing, but not
      fixable within it: the spec pins `Hexalith.Folders.Tests.csproj` to baseline, so the sanctioned
      `tests/shared` + `<Compile Include>` route is closed, and the alternative -- a shared helper in
      `src/Hexalith.Folders.Testing` -- is a public API addition to a packable library. Belongs with the
      nine-call-site corpus-reader consolidation already on the ledger.
    location: >-
      tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs and
      tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** `FolderArchiveMetadataLeakageTests` exercises archive evidence filtering with three hand-picked actor values and scans the shared sentinel corpus only against safe factory defaults. It does not serialize the real `FolderCommandRejected` event, so regressions in validator rejection or rejection-event canonicalization can escape direct corpus-driven coverage.

**Approach:** Drive every `audit-leakage-corpus.json` sentinel directly through the archive actor-evidence validator and embed each sentinel in deliberately noncanonical inputs for every caller-controlled `FolderCommandRejected` field. Serialize both rejected results and rejection events and prove that the raw sentinel is absent while preserving the existing production canonicalization behavior.

## Boundaries & Constraints

**Always:** Use the authoritative shared fixture; execute one xUnit theory row per nonblank `sentinel_samples[].value`; pass the raw corpus value to the archive actor-evidence seam; include the raw value inside a deterministic noncanonical wrapper for every rejection-event command/identifier input; keep assertion diagnostics metadata-only; preserve existing accepted-surface checks.

**Block If:** Direct coverage requires changing production canonicalization, the corpus contract, or the `FolderCommandRejected` public construction surface rather than exercising the existing seams.

**Never:** Edit the deferred-work ledger or corpus, change production behavior, hand-edit generated artifacts, initialize nested submodules, or add provider/network/runtime dependencies.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Archive validator corpus row | One raw corpus sentinel as `actorPrincipalId` on an otherwise valid archive command | `MalformedEvidence`, no actor echo in the serialized result, and no events | Failure output identifies only the invariant, not the hostile value |
| Rejection-event corpus row | One sentinel embedded in a noncanonical value supplied to command type and every nullable identifier field | Identifier properties are null, command type is the fixed unknown sentinel, and serialized JSON omits the corpus value | Existing `Create` canonicalization remains the sole sanitizer |
| Existing archive surfaces | One corpus sentinel checked against accepted event/audit/projection/problem/log/trace/metric/client surfaces | Every surface remains free of that sentinel | Surface name may identify a failing channel; sentinel text must not be echoed in custom diagnostics |

</intent-contract>

## Code Map

- `tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs` -- NEW. Sole home for `FolderCommandRejected` corpus coverage. This project is already pinned by `ScaffoldContractTests` to exactly `["Hexalith.Folders.Server", "Hexalith.Folders.Testing"]`, already loads `tests/fixtures/audit-leakage-corpus.json` (see `AuditEndpointsSentinelTests`, `FileContextEndpointTests`), and already reaches `FolderResultCode` transitively -- so the rejection-event rows land here with **no project-reference change anywhere**.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs` -- keeps only the archive actor-evidence theory and the accepted-surface theory. It must not name any `Hexalith.Folders.Server` type; the pure-domain unit lane stays free of ASP.NET Core, EventStore, Tenants and Memories.
- `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj` -- **must remain byte-identical to baseline**. Adding `Hexalith.Folders.Server` here is the known-bad state (see Spec Change Log 2026-08-29).
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:185` -- read-only gate. `AssertReferences` ends in `ShouldBe(..., ignoreOrder: true)`, i.e. an exact set. Do not amend the policy list to accommodate a test-project reference.
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs` -- read-only production evidence, per field:
  - `CanonicalIdentifierOrNull` (`:172-183`) drops any identifier that is blank, longer than `MaxCanonicalIdentifierLength` (128), or fails `^[a-z0-9._-]+$`. This is the rule the regression rows must observe.
  - `NormalizeCommandTypeForRejection` (`:139-168`) passes through the nine known canonical command types **and** any value matching the relaxed `^[A-Za-z0-9._-]+$` under 128 chars; only other shapes collapse to `UnknownCommandTypeSentinel`. Uppercase pass-through is deliberate -- do not assert that a raw uppercase sentinel is canonicalized here.
  - `code` is not caller-controlled (every emit site passes `enumValue.ToString()`); its `ArgumentException` message is an internal programming-error path, not a caller-reachable surface. Out of scope.
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:78-83,857-883` and `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs:244-317` -- read-only evidence: unsafe archive evidence returns `MalformedEvidence`, and rejected results retain only safe passthrough fields.
- `tests/fixtures/audit-leakage-corpus.json` -- read-only authoritative source of 18 `sentinel_samples[].value` rows, each with a stable `id`. Exactly one value, `id: repository-name-metadata` (`synthetic-repository-name`), is itself canonical-identifier-shaped. Do not copy, filter by category, or modify it.
- `tests/fixtures/quarantine/safety-negative-controls.json` -- read-only. Each entry pairs `sample_id` with a `contaminated_payload`; use it as the positive control that the leakage detector can actually fire.

## Tasks & Acceptance

**Execution:**
- [ ] `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj` -- leave unchanged; confirm no `Hexalith.Folders.Server` reference.
- [ ] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs` -- convert the actor-evidence and accepted-surface checks to corpus-backed theories labelled by corpus `id`; harden the JSON leakage walk; keep every assertion metadata-only.
- [ ] `tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs` -- new class carrying the wrapper rows, the raw-sentinel identifier rows, the named shape rows, and the positive controls.

**Acceptance Criteria:**
- Given every nonblank corpus sentinel supplied raw as `actorPrincipalId`, plus the three retained legacy actor values (`github_pat_credential_material`, `principal-token`, `principal@example.com`), when the archive command is handled, then the result is `MalformedEvidence`, no event is appended, and the serialized result contains no raw sentinel.
- Given every nonblank corpus sentinel embedded in the deterministic wrapper `noncanonical::{sentinel}::value` and supplied to `commandType` and every nullable identifier, when `FolderCommandRejected.Create` constructs and serializes the event, then every identifier is null, `CommandType` is `unknown_command_type`, and the decoded JSON contains neither the sentinel nor the wrapper text.
- Given every corpus sentinel **except** `id: repository-name-metadata`, when the **raw** value is supplied to every nullable identifier of `FolderCommandRejected.Create`, then each identifier is null and the decoded JSON contains no raw sentinel. The one exclusion is expressed by corpus `id` with an inline comment recording why (`synthetic-repository-name` matches `^[a-z0-9._-]+$` and is indistinguishable from a legitimate folder identifier, so dropping it is not a production invariant). This criterion is the regression trap: widening `SafeIdentifierRegex` to accept uppercase, or removing the length cap, must turn it red.
- Given the two shape threats named in `FolderCommandRejected`'s own header comment, when an identifier containing CR/LF and an identifier of `MaxCanonicalIdentifierLength + 1` canonical characters are supplied, then both are dropped to null.
- Given a legitimate canonical call -- `commandType: FoldersServerModule.ArchiveFolderCommandType` with lowercase-canonical identifiers -- when `Create` runs, then every value is preserved verbatim. Without this positive control the drop assertions are equally satisfied by a factory that destroys everything.
- Given a `contaminated_payload` from `tests/fixtures/quarantine/safety-negative-controls.json` and a payload carrying its `sample_id` sentinel in a JSON **property name**, when the leakage detector inspects them, then it reports contamination in both. Without this the entire sweep can pass vacuously.
- Given the accepted archive safety surfaces, when every corpus row executes, then all surfaces remain sentinel-free; the JSON-surface name set is asserted to be a subset of the surface-map keys, and the detector treats non-JSON input and non-string JSON values as raw text rather than throwing or skipping them.
- Given the full change, when `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` runs, then it passes with the policy list unmodified, and no production, corpus, generated-artifact, or deferred-ledger file is changed.

## Spec Change Log

### 2026-08-29 -- Rehome rejection-event coverage and make it corpus-sensitive

**Triggering findings.**
1. `[high]` The previous Code Map directed adding a `Hexalith.Folders.Server` `ProjectReference` to `tests/Hexalith.Folders.Tests`. That broke `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection`, which pins that project to an exact reference set. Reproduced: *"Hexalith.Folders.Tests references drifted from policy"*, actual set includes `Hexalith.Folders.Server`. The previous Verification section only ran `Hexalith.Folders.Tests`, so the gate -- which lives in a sibling project -- was never executed.
2. `[high]` The rejection-event theory was corpus-shaped but not corpus-sensitive. `noncanonical::{sentinel}::value` fails both `^[a-z0-9._-]+$` and `^[A-Za-z0-9._-]+$` on the `::` alone, for every sentinel, so all 18 rows collapsed onto one branch. Widening `SafeIdentifierRegex` to `^[A-Za-z0-9._-]+$` leaves the suite green while uppercase corpus sentinels would be echoed verbatim on the wire -- exactly the regression class the Intent's Problem statement says must stop escaping.
3. `[medium]` No positive control: nothing proved the leakage detector can fire, and nothing distinguished "canonicalizes" from "destroys every field".
4. `[medium]` The detector threw `JsonException` on non-JSON input, skipped non-string JSON values, and keyed JSON surfaces off an unvalidated parallel list of magic strings.
5. `[low]` Rows were labelled by enumeration position, so inserting a corpus sample silently reassigns every downstream label; the corpus's own stable `id` is the sanctioned provenance representation.
6. `[low]` The metadata-only assertion idiom carried no rationale, inviting an "simplify assertion" quick-fix to silently reopen the leak.

**What was amended.** Code Map, Tasks & Acceptance, and Verification only. `<intent-contract>` is untouched: the mandated wrapper rows remain, and the raw-sentinel rows are added alongside them, which the contract permits and its Problem statement requires.

**Known-bad state to avoid.** Do not add `Hexalith.Folders.Server` to `tests/Hexalith.Folders.Tests.csproj`, and do not amend the `ScaffoldContractTests` policy list to make such a reference legal. Do not assert that a raw uppercase sentinel is canonicalized in `commandType` -- that pass-through is deliberate production behavior, and asserting otherwise would force a production change the Intent forbids.

**KEEP instructions (must survive re-derivation).**
- Corpus-backed `TheoryData<string>` built from `TheoryDataRow<string>` with a `Label`, so no sentinel text reaches an xUnit display name. Switch the label source from a positional counter to the corpus `id`.
- The three legacy inline actor values retained as labelled rows -- they are the only rows that isolate the token/word blocklist rather than the charset gate.
- The metadata-only assertion idiom `(x is null).ShouldBeTrue("...")` / `.ShouldBeFalse("...")` in place of `ShouldBeNull()` / `ShouldNotContain()`, because Shouldly renders the actual value into the failure message. Add a comment stating this so it is not "simplified" away.
- The recursive decoded-JSON walk over property names and string values.
- The mandated `noncanonical::{sentinel}::value` wrapper rows.
- No production, corpus, generated-artifact, or deferred-ledger edits.

## Review Triage Log

### 2026-08-29 -- Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 3, low 0)
- defer: 0
- reject: 18: (high 0, medium 9, low 9)
- addressed_findings:
  - `[medium]` `[patch]` Restored the three pre-existing unsafe actor cases as safely labelled theory rows alongside all corpus rows, preserving the token-only validator regression that the corpus does not isolate.
  - `[medium]` `[patch]` Replaced raw-only serialized JSON substring checks with recursive decoded-JSON inspection so escaped property names or string values cannot produce a false green result.
  - `[medium]` `[patch]` Replaced the event collection assertion with a metadata-only count assertion so a failing leakage test does not print a hostile event payload.

### 2026-08-29 -- Review pass (1)
- intent_gap: 0
- bad_spec: 7: (high 2, medium 3, low 2)
- patch: 0
- defer: 4: (high 0, medium 2, low 2)
- reject: 9: (high 0, medium 1, low 8)
- addressed_findings:
  - `[high]` `[bad_spec]` The Code Map's `Hexalith.Folders.Server` project reference broke `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` (reproduced red). Rehomed all `FolderCommandRejected` coverage to `tests/Hexalith.Folders.Server.Tests`, which already carries that reference; the csproj now stays at baseline.
  - `[high]` `[bad_spec]` The `noncanonical::{sentinel}::value` wrapper made every rejection row sentinel-independent, so a widening of `SafeIdentifierRegex` kept the suite green. Added raw-sentinel identifier rows, with the single canonical-shaped corpus value excluded by `id` and justified inline.
  - `[medium]` `[bad_spec]` Added positive controls: the quarantine negative-control payloads must trip the detector, and a fully canonical `Create` call must preserve every value.
  - `[medium]` `[bad_spec]` Added the CR/LF and over-length identifier rows named as threats in `FolderCommandRejected`'s own header comment; the 128-char cap was previously unreachable behind the charset check.
  - `[medium]` `[bad_spec]` Hardened the leakage detector: non-JSON input and non-string JSON values are scanned as raw text instead of throwing or being skipped, and the JSON-surface name set is asserted against the surface-map keys.
  - `[low]` `[bad_spec]` Theory rows are labelled by the corpus's stable `id` instead of a positional counter.
  - `[low]` `[bad_spec]` The metadata-only assertion idiom must carry an inline rationale so it is not "simplified" back into value-printing assertions.

### 2026-08-29 -- Review pass (2)
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 3, low 2)
- defer: 4: (high 1, medium 3, low 0)
- reject: 15: (high 0, medium 2, low 13)
- addressed_findings:
  - `[medium]` `[patch]` Corrected three false or overstated comments: the legacy actor rows do not lack a corpus equivalent (`repository-name-metadata` is also charset-valid and substring-blocked), the duplication is recorded in this spec's `deferred:` list and not in the deferred-work ledger, and the accepted-surface theory scans constant payloads rather than injected sentinels.
  - `[medium]` `[patch]` The non-string-JSON self-test was vacuous -- `ContainsSentinel` raw-scans first, so the walk's `default:` arm was never reached and deleting it left both suites green. Split into a raw-text test and one that calls the element walk directly on parsed numeric and boolean nodes; removing the arm now reddens each suite.
  - `[medium]` `[patch]` Added two characterization rows: the documented `CommandType` pass-through for relaxed-shape values (previously unpinned, so a policy change would have been silent) and the preservation of the one canonical-shaped corpus sentinel that the raw-drop theory excludes.
  - `[low]` `[patch]` Guarded the hard-coded corpus id lookups with a named metadata-only failure instead of a bare `Single()`, and filtered blank ids so they cannot collapse into one duplicate row label.
  - `[low]` `[patch]` Replaced the ASCII-only `EscapeFirstCharacter` with a full `\uXXXX` encoder plus a well-formedness check, so a sentinel starting with a quote, backslash, control character, or surrogate cannot silently produce invalid JSON and misattribute the failure to the negative control.

### 2026-08-29 -- Review pass (3)
- intent_gap: 0
- bad_spec: 0
- patch: 7: (high 0, medium 3, low 4)
- defer: 4: (high 2, medium 2)
- reject: 25: (high 0, medium 6, low 19)
- addressed_findings:
  - `[medium]` `[patch]` The length-cap coverage was asserted against itself -- `overLength`, `atLimit` and the pass-through predicate all derive from `MaxCanonicalIdentifierLength`, so raising the cap moved input and expectation in lockstep. Verified: setting the constant to 256 left every row green before the fix. Pinned the literal `128`; the same mutation now reddens `NamedShapeThreatsAreDroppedFromRejectionIdentifiers`.
  - `[medium]` `[patch]` The `JsonArchiveSurfaceNames` guard was inert: `ContainsSentinel` decides JSON-vs-raw by parsing, so the `ContainsKey` check protected nothing and deleting the list changed no scan. Replaced with `ArchiveSurfaceMapKeepsDeclaredJsonSurfacesParseable`, a `[Fact]` pinning both directions -- every declared surface must still parse, and every parseable surface must be declared. Both directions mutation-verified red.
  - `[medium]` `[patch]` Three `CommandType` assertions used `ShouldBe`, which renders the *actual* value into the failure message -- and on the regression those rows exist to catch, that actual value is the hostile sentinel. Converted to the file's own metadata-only idiom. Re-ran the mandated mutation and scanned the failure output against all 18 corpus values: zero sentinel text.
  - `[low]` `[patch]` Corrected two false header claims: the raw rows do not trap cap removal (the longest corpus value is 46 characters, far under the 128 cap), and `Create` is not the repository's "sole sanitizer" -- two sibling rejection events call the shared helpers directly.
  - `[low]` `[patch]` Added duplicate-id guards to both fixture loaders in both files: ids are theory row labels and the `SentinelById` / `Single()` lookup key, so duplicates would collide labels and silently resolve only the first match.
  - `[low]` `[patch]` The negative-control loader filtered blank `id` and `contaminated_payload` but not blank `sample_id`, so such a row was generated and then died inside `SentinelById` with a message blaming the corpus. Added to the filter.
  - `[low]` `[patch]` The wrapped rows could pass for the wrong reason -- a corpus sample over 107 characters pushes the wrapper past the length cap, retiring that row's shape coverage silently. Added an explicit cap assertion on the wrapped value.

## Auto Run Result

Status: done
Blocking condition: none

### Implemented change

Follow-up review pass over the archive metadata-leakage regression coverage delivered by the two prior passes. No spec amendment and no code re-derivation were needed: the implementation matches the spec's chosen reading, and every finding this pass was either a contained hardening of the new tests or a pre-existing repository gap. Seven patches were applied to the two test files; four findings were deferred, two of them high severity.

The substantive change is that three of this story's own assertions were provably unable to fail (self-referential length cap, inert JSON-surface guard) or could leak on failure (value-printing `CommandType` assertions). All three are now mutation-verified.

### Files changed

- [../../tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs](../../tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs) -- literal cap pin, metadata-only `CommandType` assertions, wrapper length guard, loader id-uniqueness and blank-`sample_id` guards, corrected header claims.
- [../../tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs](../../tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs) -- new `ArchiveSurfaceMapKeepsDeclaredJsonSurfacesParseable` structural `[Fact]` replacing the inert per-row presence check, plus the same loader guards.
- [spec-archive-leakage-regression-coverage.md](spec-archive-leakage-regression-coverage.md) -- this pass's triage log, four deferred findings, and this result.

No production, corpus, generated-artifact, CI-script, or deferred-ledger file was changed; `tests/Hexalith.Folders.Tests.csproj` remains byte-identical to baseline `34fed52`.

### Review findings breakdown

Four review layers (blind hunter, edge-case hunter, verification-gap, intent-alignment) produced 36 distinct findings after deduplication: 7 patched (3 medium, 4 low), 4 deferred (2 high, 2 medium), 25 rejected (6 medium, 19 low). No intent gap and no bad spec.

Both high-severity deferrals were verified against the repository, not taken on report:

1. `tests/Hexalith.Folders.Server.Tests` -- the home this story's spec chose for the rejection-event trap -- runs in **no** CI gate lane. Confirmed by exhausting every `$unitTestProjects` list in `tests/tools/*.ps1` and every `.github/workflows/*.yml`. The trap reddens locally under the mandated mutation and stays green in CI. Enrolment was attempted in this pass and deliberately reverted: it requires editing the generated gate report `_bmad-output/gates/baseline-ci/latest.json` (pinned by a conformance test) and depends on the production format break below, both forbidden by AC8. It is a 644-test, repository-wide gap that deserves its own story rather than a side effect of this one.
2. The baseline lane is red on `main` regardless: `dotnet format whitespace --verify-no-changes` fails with 13 errors on `ProviderOperationSourceResolutionResult.cs`, untouched here and last changed by be36435 on 2026-08-26. The `format` gate precedes and gates `unit-tests`.

### Follow-up review recommendation

`true`. Patched findings this pass: high 0, medium 3, low 4; score `3 x 3 + 1 x 4 = 13`, which is 5 or more.

### Verification performed

- `dotnet build Hexalith.Folders.slnx --no-restore` -- 0 warnings, 0 errors.
- `dotnet test tests/Hexalith.Folders.Tests` -- 1736/1736 passed, 0 skipped (1735 at pass start, +1 for the new structural `[Fact]`).
- `dotnet test tests/Hexalith.Folders.Server.Tests` -- 644/644 passed, 0 skipped.
- `dotnet test tests/Hexalith.Folders.Testing.Tests` -- 66/66 passed, including `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` with the policy list unmodified.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --filter BaselineCiWorkflowConformanceTests` -- 8/8 passed, confirming the reverted CI-enrolment attempt left no residue.
- `dotnet format whitespace ... --include ./tests/Hexalith.Folders.Tests/ ./tests/Hexalith.Folders.Server.Tests/` -- clean.
- **Mandated mutation check (performed and reverted):** widening `SafeIdentifierRegex` from `^[a-z0-9._-]+$` to `^[A-Za-z0-9._-]+$` turns **13** raw-sentinel rows red. The full failure output was scanned programmatically against all 18 corpus values: **zero** sentinel text present, so the metadata-only contract holds through the new assertion idiom.
- **Additional mutations (performed and reverted):** raising `MaxCanonicalIdentifierLength` to 256 reddens `NamedShapeThreatsAreDroppedFromRejectionIdentifiers` (it did not before this pass); making a declared JSON surface unparsable, and making an undeclared surface parse as JSON, each redden `ArchiveSurfaceMapKeepsDeclaredJsonSurfacesParseable`.
- `git diff --stat 34fed52 -- src/ tests/fixtures/` -- empty. `git diff --check` -- clean. `tests/Hexalith.Folders.Tests.csproj` -- 0-line diff against baseline.

### Residual risks

- **The regression trap does not gate CI.** This is the dominant residual risk and is deferred above with full evidence. Until `Hexalith.Folders.Server.Tests` is enrolled in a lane, the rejection-event half of this story protects local runs only.
- Detector and fixture-reader duplication across the two test files remains, blocked by the spec's baseline csproj pin; deferred above.
- The accepted-archive-surface sweep still cannot fail (deferred earlier); real coverage on this change comes from the two direct-seam theories.
- `CommandType` legitimately echoes any relaxed-shape caller value under the cap. Deliberate production behavior, now characterized by a test, but an uppercase corpus sentinel can still appear there.
- Coverage stays at the unit seams named by the intent; the `FolderDomainProcessor` argument mapping and the two sibling rejection events remain untested (deferred above).

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx --no-restore` -- expected: zero warnings and zero errors.
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore` -- expected: the complete core test project passes.
- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- expected: the complete server test project passes, including the new `FolderCommandRejectedLeakageTests`.
- `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore` -- expected: `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` passes with the policy list unmodified.
- `git diff --stat -- src/ tests/fixtures/ _bmad-output/implementation-artifacts/deferred-work.md` -- expected: empty; no production, corpus, or ledger change.
- `git diff --check` -- expected: no whitespace errors or conflict markers.

**Mutation check (must be performed and reverted):** temporarily widen `SafeIdentifierRegex` in `src/Hexalith.Folders.Server/FolderCommandRejected.cs` from `^[a-z0-9._-]+$` to `^[A-Za-z0-9._-]+$`, run `Hexalith.Folders.Server.Tests`, and confirm the raw-sentinel rows turn **red**. Revert the mutation and confirm `git diff -- src/` is empty before finalizing.
