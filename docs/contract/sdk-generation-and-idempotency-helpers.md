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

## Downstream Ownership

Story 1.13 owns final C13 parity rows and must consume generated operation IDs and helper entry points instead of reimplementing hash construction.

Story 1.14 owns CI golden-file and drift gate wiring.

Epic 4 owns runtime idempotency persistence and lifecycle behavior.

Epic 5 owns SDK convenience helpers, CLI wrappers, and MCP wrappers, including any future `UploadFileAsync(stream)` convenience API.
