# Test Automation Summary — Story 5.1 (SDK convenience helpers, samples, quickstart)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-05-27
**Engineer:** QA automation (Jerome)
**Feature under test:** `Hexalith.Folders.Client` convenience layer (`src/Hexalith.Folders.Client/Convenience/`) + the runnable `Hexalith.Folders.Sample` golden-flow driver and its `BearerTokenHandler`.

> The prior run's Story 4.17 summary was preserved as `4-17-test-summary.md` before this file was overwritten (this canonical file holds the latest QA run).

## Framework Detected

xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` (.NET 10). HTTP behavior is exercised with hand-written fake `HttpMessageHandler`s (no live server, no Dapr/Keycloak/Redis/network) — hermetic, per the story's AC #7 and the project testing rules. No UI exists in this story (SDK + sample only), so "E2E" coverage is the canonical lifecycle driven through `IClient`.

## Generated / Expanded Tests

All gaps were auto-applied into the existing story test files to reuse their fake handlers and helpers.

### API / unit tests — `tests/Hexalith.Folders.Client.Tests/FileUploadConvenienceTests.cs` (+11 cases)
- `UploadStreamedFileAsyncRoutesStreamedBodyAndHeaderTripleToAddFile` — the previously **untested** `UploadStreamedFileAsync` extension routes to `/files/add` with a `PutFileStream` body, the header triple, and no field outside the spine.
- `UploadStreamedFileAsyncChangeRoutesToChangeFile` — Change kind routes to `/files/change`.
- `UploadFileAsyncFromStreamUploadsInlineWhenAtOrBelowBoundary` — the `Stream` overload **happy path** (only the over-boundary rejection was covered before); asserts inline body + `byteLength` = decoded length + round-trips the base64 content.
- `UploadFileAsyncDoesNotTranslateNon413ServerErrors` — a `500` propagates as `HexalithFoldersApiException`, proving the `when (StatusCode == 413)` filter does not over-capture.
- `UploadFileAsync413TranslationPreservesOriginatingApiException` — the `413` translation wraps the originating API exception as `InnerException` and the message leaks no byte limit.
- `UploadFileAsyncRejectsBlankHeaderTripleWithoutCallingServer` (Theory ×3) — blank idempotency key / correlation / task fails closed with `ArgumentException` **before** any wire call.
- `BuildInlineSetsContentMediaTypeAndSupportsChangeKind`, `BuildInlineAcceptsEmptyContentAtZeroLength`, `BuildStreamedRejectsRemoveOperationKind`, `BuildStreamedRejectsMissingStagingEvidenceFields`, `ComputeIdempotencyKeyValidatesArguments` — builder edge/validation coverage.

### Unit tests — `tests/Hexalith.Folders.Client.Tests/CorrelationAndTaskIdTests.cs` (+2 cases)
- `NewCorrelationIdEncodesCurrentTimestampInTheUlidPrefix` — decodes the first 10 Crockford Base32 chars and asserts the embedded 48-bit timestamp falls within the call window (validates ULID time semantics deterministically — **no sleep**).
- `AddFoldersCorrelationIdProviderDoesNotReplaceAnAlreadyRegisteredProvider` — `TryAdd` first-registration-wins semantics.

### E2E / sample tests — `samples/Hexalith.Folders.Sample.Tests/FolderLifecycleSampleTests.cs` (+7 cases)
- `ExplicitCorrelationIdPropagatesToEveryRequest` — an explicit correlation ID appears on **every** request across the full golden flow (adapter parity).
- `RegisteredCorrelationProviderSuppliesCorrelationIdWhenNoneIsExplicit` — exercises the previously **untested** `ICorrelationIdProvider` constructor path of the sample.
- `LifecycleFailsClosedWhenTaskIdMissingBeforeAnyRequest` — blank task ID throws `InvalidOperationException` before any HTTP call.
- `BearerTokenHandlerAttachesBearerAuthorizationWhenTokenPresent` + `BearerTokenHandlerOmitsAuthorizationWhenTokenBlank` (Theory ×3) — the previously **untested** sample auth handler.

## Coverage

| Surface | Before | After |
|---|---|---|
| `FileUpload` builders | builders + parity + idempotency | + media-type/empty/streamed-remove/staging-validation/arg-validation |
| `FoldersFileUploadExtensions` | inline add/change, replay, conflict, 413, stream rejection | + **streamed upload (add/change)**, **stream happy path**, **non-413 passthrough**, **413 inner-exception**, **blank-triple fail-closed** |
| `CorrelationAndTaskId` + DI | precedence + ULID shape + DI register | + **ULID timestamp encoding**, **TryAdd idempotency** |
| Sample lifecycle / auth | composition + ordered golden flow | + **correlation propagation (explicit & provider)**, **fail-closed task ID**, **BearerTokenHandler** |

New test cases this run: **24** (Client.Tests +17, Sample.Tests +7).

## Test Run Results

Run with the `global.json`-matching SDK (`~/.dotnet/dotnet` 10.0.300; the PATH SDK 10.0.108 does not satisfy `global.json`).

- `tests/Hexalith.Folders.Client.Tests` — **71 passed / 1 failed / 72 total**
- `samples/Hexalith.Folders.Sample.Tests` — **10 passed / 0 failed / 10 total**

### Known pre-existing failure (NOT a Story 5.1 regression)
`ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` fails on a 6-line **whitespace-only** diff between the committed (`.editorconfig`-normalized) generated client and a raw NSwag regeneration. This is documented in the story Debug Log, is independent of the convenience layer (no `Generated/` inputs were touched), and cannot be resolved without editing generated code (forbidden by this story). A second generation test (`HelperGenerationTargetRegeneratesWhenContractSpineChanges`) only failed when a spawned child `dotnet` resolved the PATH SDK; it passes once the 10.0.300 SDK leads `PATH`.

## Commands Run

```text
PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet dotnet test tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --nologo
PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet dotnet test samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj --nologo
```

## Next Steps
- Run in CI with the `global.json` SDK leading `PATH` so generation tests spawn the correct child SDK.
- Live end-to-end against a running AppHost (`FOLDERS_BASE_ADDRESS`) remains an opt-in manual/integration lane, not a blocking unit gate.
- The pre-existing whitespace generation diff is a Contracts/Epic-1 concern (regenerate + commit normalized output), tracked outside Story 5.1.
