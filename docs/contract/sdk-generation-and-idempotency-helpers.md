# SDK Generation and Idempotency Helpers

Status: Story 1.12 implementation note.

## Generated Artifacts

NSwag generates the typed client and DTO surface from:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
```

The generated output lives under:

```text
src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs
src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs
```

Generated files are not manually customized. Hexalith-specific helper logic is emitted as companion partials and shared helper code under `src/Hexalith.Folders.Client/Idempotency/`.

## Rerun Command

```text
dotnet msbuild src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersClient
```

The client project also runs the generation target before compile when the generated client is stale or missing.

## Deterministic Output Policy

The generator uses `src/Hexalith.Folders.Client/nswag.json` with stable namespace, class names, nullable settings, collection types, injected `HttpClient`, no sync methods, and no generated base URL. Generated output must not contain timestamps, machine-local paths, production URLs, payload values, provider tokens, credential material, or unauthorized resource hints.

The current generated helper provenance stores:

- Contract Spine SHA-256
- generation configuration SHA-256
- operation IDs
- helper field names from `x-hexalith-idempotency-equivalence`

Stale-output detection compares current repository input content hashes rather than timestamps or local paths.

## Hash Format

`ComputeIdempotencyHash()` returns:

```text
sha256:<lowercase-hex-digest>
```

The digest is SHA-256 over UTF-8 canonical lines:

```text
operation=<OperationId>
field=<lexicographic_field_path>;present=<true|false>;value=<typed_value_or_omitted>
```

Scalar formatting is culture-invariant. Strings escape line breaks, backslashes, and field separators. Enum values use OpenAPI wire values. Objects are serialized as sorted JSON. Explicit null and omitted values are distinct when the companion helper can observe presence; otherwise callers must use the generated presence companion property.

`DateTime` values must have `Kind=DateTimeKind.Utc`; non-UTC kinds throw `InvalidOperationException` because the host timezone would otherwise leak into the canonical form. `DateTimeOffset` values must have `Offset == TimeSpan.Zero`; non-zero offsets throw the same exception. Producers convert to UTC before passing values into idempotency-equivalence fields; the hasher rejects rather than silently normalizes so machine-dependent hashes cannot arise from local-time inputs.

Decimal values are encoded with their sign, scale, and 96-bit integer components rather than a display string. This preserves distinctions such as `1m`, `1.0m`, and `1.00m` if a future spine field declares decimal scale as semantically meaningful.

The Contract Spine currently declares `path_metadata` and `path_policy_class` as separate equivalence fields for file mutations. The helper therefore includes `pathPolicyClass` once inside the canonical `path_metadata` JSON object and once as the top-level `path_policy_class` scalar; this is intentional spine-driven redundancy, not SDK-side field invention.

## Helper Signature Versioning

Helper method parameter lists are derived from the Contract Spine path-parameter declaration order plus the operation's `x-hexalith-idempotency-equivalence` list. Spine evolution can therefore reorder or rename helper parameters. Two parameter-set changes shipped with the Round 3 generator regeneration on 2026-05-16:

- `PrepareWorkspaceRequest.ComputeIdempotencyHash`, `LockWorkspaceRequest.ComputeIdempotencyHash`, and `ReleaseWorkspaceLockRequest.ComputeIdempotencyHash` parameters now follow `(folderId, workspaceId, taskId)` to match the spine path declaration. The prior shape `(folderId, taskId, workspaceId)` is no longer emitted; positional callers using the old order will silently swap `workspaceId` and `taskId` into the wrong canonical-line slots and produce a divergent hash.
- `BranchRefPolicyRequest.ComputeIdempotencyHash(string folderId)` takes only the path-bound `folderId`; `repositoryBindingId` is sourced from the `RepositoryBindingId` DTO property and is no longer a method parameter. The prior `providerBindingRef` parameter is no longer emitted.

Downstream consumers should detect the rename surface by reading the generated `HexalithFoldersGeneratedArtifacts.HelperSchemaVersion` constant. The constant is a 16-hex-character (64-bit) prefix of a SHA-256 derived from the helper signature shape: ordered schema name, ordered parameter names, ordered idempotency field paths. Parameter TYPES are not included in the hash input; today all generated parameters are `string`, and a future spine change that retypes a parameter (e.g. `string` → `Guid`) leaves the constant unchanged. The constant is intended as a *fingerprint* for detecting shape change between SDK versions, not as a versioned shape registry — consumers must use it to gate compatibility checks (e.g. "does my cached call site still match the SDK I'm linked against?") rather than as a switch-key for runtime shape resolution. A stable spine leaves the constant unchanged. The constant is not part of the runtime idempotency hash and is never serialized as helper input.

## Downstream Ownership

Story 1.13 owns final C13 parity rows and must consume generated operation IDs and helper entry points instead of reimplementing hash construction.

Story 1.14 owns CI golden-file and drift gate wiring.

Epic 4 owns runtime idempotency persistence and lifecycle behavior.

Epic 5 owns SDK convenience helpers, CLI wrappers, and MCP wrappers, including any future `UploadFileAsync(stream)` convenience API.
