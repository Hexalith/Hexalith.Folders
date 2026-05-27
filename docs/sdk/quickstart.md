# Hexalith.Folders SDK Quickstart

Status: Story 5.1 implementation note.

This guide shows how to consume the canonical Folders lifecycle through the typed SDK
(`Hexalith.Folders.Client`) and its convenience helpers, without learning internal transport details.

All examples are **metadata-only**: identifiers are opaque, synthetic references. Never place secrets,
tokens, raw file contents, diffs, provider payloads, or local absolute paths in requests, logs, or examples.

Related contract docs:

- [SDK generation and idempotency helpers](../contract/sdk-generation-and-idempotency-helpers.md) — the
  generation pipeline, the `sha256:<hex>` hash format, and `HelperSchemaVersion`.
- [Idempotency and parity rules](../contract/idempotency-and-parity-rules.md) — replay/conflict and
  adapter-parity semantics the helpers preserve.

## 1. Register the typed client (DI)

`AddFoldersClient` registers the typed `IClient` as a typed `HttpClient`. Authentication is intentionally
outside the SDK: attach a bearer-token `DelegatingHandler` on the returned `IHttpClientBuilder`.

```csharp
using Hexalith.Folders.Client;
using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;

// Bind options from the "Folders" configuration section...
services.AddFoldersClient();

// ...or configure explicitly, chaining your own auth handler:
services
    .AddFoldersClient(options => options.BaseAddress = new Uri("https://folders.internal/"))
    .AddHttpMessageHandler<MyBearerTokenHandler>();
```

`FoldersClientOptions.BaseAddress` must be an absolute URI (the transport endpoint only; it is never tenant
authority). A bearer-token handler looks like:

```csharp
public sealed class MyBearerTokenHandler(ITokenSource tokenSource) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string token = await tokenSource.GetAccessTokenAsync(ct);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
```

## 2. Source correlation and task IDs

Mutating operations carry the header triple `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`.
`CorrelationAndTaskId` implements the Adapter Parity Contract sourcing precedence:

```csharp
using Hexalith.Folders.Client.Convenience;

// Correlation: explicit value → registered ICorrelationIdProvider → fresh SDK-generated ULID.
string correlationId = CorrelationAndTaskId.ResolveCorrelationId(explicitCorrelationId: null);

// Task ID: caller-provided only. The SDK never generates it and never coerces a missing value to empty.
string taskId = CorrelationAndTaskId.ResolveTaskId("task_01HZY7Z6N7J4Q2X8Y9V0TSK001");
```

To register a correlation-ID provider (consulted before the ULID fallback):

```csharp
services.AddFoldersCorrelationIdProvider<MyCorrelationIdProvider>();
```

The provider never affects task-ID or idempotency-key sourcing.

## 3. Upload a file with the convenience helper

The upload helper selects the inline-vs-streamed transport for you and builds the polymorphic
`FileMutationRequest` body. Content at or below the inline boundary (262144 bytes) uses the inline transport.

```csharp
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

var descriptor = new FileUploadDescriptor
{
    FolderId = "folder_01HZY7Z6N7J4Q2X8Y9V0FLD001",
    WorkspaceId = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001",
    OperationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
    MediaType = "text/markdown",
    PathMetadata = new PathMetadata
    {
        NormalizedPath = "docs/readme.md",
        DisplayName = "readme.md",
        PathPolicyClass = "metadata_only",
        UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
    },
    FileOperationKind = FileMutationRequestFileOperationKind.Add, // or Change
};

ReadOnlyMemory<byte> content = /* your decoded bytes or a Stream overload */;

await client.UploadFileAsync(descriptor, content, idempotencyKey, correlationId, taskId, cancellationToken);
```

The helper never adds any field, state, operation, or behavior absent from the Contract Spine: it only
selects an existing transport shape. For content **above** the inline boundary it throws
`FileUploadStreamingRequiredException` (the client-side equivalent of the server `413` +
`X-Hexalith-Retry-Transport: stream` contract) — stage the content out-of-band and submit it streamed:

```csharp
var staging = new FileStreamStagingEvidence
{
    StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
    ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
    ObservedLength = 524288,
};

await client.UploadStreamedFileAsync(descriptor, staging, idempotencyKey, correlationId, taskId, cancellationToken);
```

## 4. Idempotency-key guidance

The SDK **never** auto-generates or substitutes an idempotency key — supplying one is an explicit caller
decision. Compute the canonical key from the request via the generated `ComputeIdempotencyHash()` (the SDK
never reimplements canonicalization). For file mutations, `FileUpload.ComputeIdempotencyKey` is the supported
one-liner:

```csharp
FileMutationRequest request = FileUpload.BuildInlineFileMutation(
    content, descriptor.MediaType, descriptor.PathMetadata, descriptor.OperationId);

string idempotencyKey = FileUpload.ComputeIdempotencyKey(request, descriptor.WorkspaceId, taskId);
```

Replay and conflict semantics are enforced server-side and surfaced through generated response types:

- Same key + equivalent payload ⇒ the same logical result; `AcceptedCommand.IdempotentReplay` is `true`.
- Same key + different payload ⇒ a `409` carrying canonical `idempotency_conflict`
  (`HexalithFoldersApiException<ProblemDetails>` with `Result.Code == "idempotency_conflict"`).

Other request DTOs expose `ComputeIdempotencyHash(...)` with parameters matching the spine path declaration
(for example, `PrepareWorkspaceRequest.ComputeIdempotencyHash(folderId, workspaceId, taskId)`).

## 5. Run the local AppHost sample

The runnable sample `samples/Hexalith.Folders.Sample` drives the canonical lifecycle (configure provider
binding → validate readiness → create a repository-backed folder → prepare workspace → lock → add a file via
the upload helper → commit → query status → inspect audit) through `IClient` and the helpers.

1. Launch the AppHost Aspire topology:

   ```text
   dotnet run --project src/Hexalith.Folders.AppHost
   ```

   The Aspire dashboard is at `https://localhost:17000`.

2. Point the sample at the Folders server endpoint shown in the dashboard and run it:

   ```text
   FOLDERS_BASE_ADDRESS=https://localhost:<folders-port> \
   dotnet run --project samples/Hexalith.Folders.Sample
   ```

Without `FOLDERS_BASE_ADDRESS` the sample prints these instructions and exits without making any network call,
so it stays usable in environments with no running services. Live end-to-end execution against the AppHost is
an opt-in manual/integration path, not a blocking unit gate.
