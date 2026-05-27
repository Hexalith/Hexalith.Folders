using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Shouldly;

using Xunit;

using GeneratedFoldersClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Client.Tests;

public sealed class FileUploadConvenienceTests
{
    // The only top-level members declared by the Contract Spine FileMutationRequest schema.
    private static readonly HashSet<string> AllowedRequestFields = new(StringComparer.Ordinal)
    {
        "requestSchemaVersion",
        "operationId",
        "pathMetadata",
        "contentHashReference",
        "fileOperationKind",
        "transportOperation",
        "byteLength",
        "inlineContent",
        "streamDescriptor",
    };

    private static readonly HashSet<string> AllowedInlineFields = new(StringComparer.Ordinal)
    {
        "mediaType",
        "contentBytes",
        "contentMediaType",
    };

    private static readonly HashSet<string> AllowedStreamFields = new(StringComparer.Ordinal)
    {
        "mediaType",
        "declaredLength",
        "observedLength",
        "stagingReference",
        "observedContentHashReference",
        "uploadMode",
    };

    private static PathMetadata SamplePath() => new()
    {
        NormalizedPath = "docs/readme.md",
        DisplayName = "readme.md",
        PathPolicyClass = "metadata_only",
        UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
    };

    [Fact]
    public void BuildInlineSelectsInlineTransportAtOrBelowBoundary()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            content,
            "text/plain",
            SamplePath(),
            "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        request.TransportOperation.ShouldBe("PutFileInline");
        request.FileOperationKind.ShouldBe(FileMutationRequestFileOperationKind.Add);
        request.ByteLength.ShouldBe(content.Length);
        request.RequestSchemaVersion.ShouldBe("v1");

        request.AdditionalProperties.ShouldContainKey("inlineContent");
        var inline = request.AdditionalProperties["inlineContent"].ShouldBeOfType<PutFileInline>();
        Convert.FromBase64String(inline.ContentBytes).ShouldBe(content);
        inline.MediaType.ShouldBe("text/plain");
    }

    [Fact]
    public void BuildInlineAcceptsExactBoundary()
    {
        byte[] content = new byte[FileUpload.InlineTransportBoundaryBytes];

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(content, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        request.TransportOperation.ShouldBe("PutFileInline");
        request.ByteLength.ShouldBe(FileUpload.InlineTransportBoundaryBytes);
    }

    [Fact]
    public void BuildInlineRejectsContentOverBoundaryWithStreamingSignal()
    {
        byte[] content = new byte[FileUpload.InlineTransportBoundaryBytes + 1];

        _ = Should.Throw<FileUploadStreamingRequiredException>(
            () => FileUpload.BuildInlineFileMutation(content, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV"));
    }

    [Fact]
    public void BuildInlineDerivesContentHashReferenceFromContent()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        request.ContentHashReference.ShouldNotBeNull();
        request.ContentHashReference.ShouldStartWith("hashref_");
        // hashref_ + 64 lowercase hex characters.
        request.ContentHashReference.Length.ShouldBe("hashref_".Length + 64);
        System.Text.RegularExpressions.Regex.IsMatch(request.ContentHashReference, "^hashref_[A-Za-z0-9]{32,96}$").ShouldBeTrue();
    }

    [Fact]
    public void BuildInlineHonorsExplicitContentHashReference()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic");
        const string explicitReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0ADD002";

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV", contentHashReference: explicitReference);

        request.ContentHashReference.ShouldBe(explicitReference);
    }

    [Fact]
    public void BuildStreamedSelectsStreamTransportAboveBoundary()
    {
        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes + 1,
        };

        FileMutationRequest request = FileUpload.BuildStreamedFileMutation(
            evidence, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV", FileMutationRequestFileOperationKind.Change);

        request.TransportOperation.ShouldBe("PutFileStream");
        request.FileOperationKind.ShouldBe(FileMutationRequestFileOperationKind.Change);
        request.ByteLength.ShouldBe(evidence.ObservedLength);
        request.ContentHashReference.ShouldBe(evidence.ObservedContentHashReference);

        var descriptor = request.AdditionalProperties["streamDescriptor"].ShouldBeOfType<PutFileStream>();
        descriptor.ObservedLength.ShouldBe(evidence.ObservedLength);
        descriptor.StagingReference.ShouldBe(evidence.StagingReference);
        descriptor.ObservedContentHashReference.ShouldBe(evidence.ObservedContentHashReference);
        descriptor.UploadMode.ShouldBe(PutFileStreamUploadMode.Request_body_stream);
    }

    [Fact]
    public void BuildStreamedRejectsContentAtOrBelowBoundary()
    {
        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => FileUpload.BuildStreamedFileMutation(evidence, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV"));
    }

    [Fact]
    public void BuildRejectsRemoveOperationKind()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic");

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => FileUpload.BuildInlineFileMutation(content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV", (FileMutationRequestFileOperationKind)2));
    }

    [Fact]
    public void InlineRequestSetsNoFieldOutsideTheContractSpine()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV", contentMediaType: "text/markdown");

        request.AdditionalProperties.Keys.ShouldBeSubsetOf(new[] { "inlineContent" });

        JObject json = JObject.Parse(JsonConvert.SerializeObject(request));
        json.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedRequestFields);
        json.ContainsKey("streamDescriptor").ShouldBeFalse();

        JObject inline = (JObject)json["inlineContent"]!;
        inline.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedInlineFields);
    }

    [Fact]
    public void StreamedRequestSetsNoFieldOutsideTheContractSpine()
    {
        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes + 1,
        };

        FileMutationRequest request = FileUpload.BuildStreamedFileMutation(evidence, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        request.AdditionalProperties.Keys.ShouldBeSubsetOf(new[] { "streamDescriptor" });

        JObject json = JObject.Parse(JsonConvert.SerializeObject(request));
        json.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedRequestFields);
        json.ContainsKey("inlineContent").ShouldBeFalse();

        JObject descriptor = (JObject)json["streamDescriptor"]!;
        descriptor.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedStreamFields);
    }

    [Fact]
    public void ComputeIdempotencyKeyMatchesGeneratedHashAndDistinguishesPayloads()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");
        const string workspaceId = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001";
        const string taskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001";

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        // The convenience never reimplements canonicalization: it returns exactly the generated hash.
        FileUpload.ComputeIdempotencyKey(request, workspaceId, taskId)
            .ShouldBe(request.ComputeIdempotencyHash(workspaceId, taskId));

        // Equivalent payload + same path => same key (replay path).
        FileMutationRequest equivalent = FileUpload.BuildInlineFileMutation(content, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        FileUpload.ComputeIdempotencyKey(equivalent, workspaceId, taskId)
            .ShouldBe(FileUpload.ComputeIdempotencyKey(request, workspaceId, taskId));

        // Different content => different content-hash reference => different key (conflict path).
        FileMutationRequest differing = FileUpload.BuildInlineFileMutation(
            Encoding.UTF8.GetBytes("a different synthetic body"), "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        FileUpload.ComputeIdempotencyKey(differing, workspaceId, taskId)
            .ShouldNotBe(FileUpload.ComputeIdempotencyKey(request, workspaceId, taskId));
    }

    [Fact]
    public async Task UploadFileAsyncAddsFileWithHeaderTripleAndInlineBody()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");
        FileUploadDescriptor descriptor = new()
        {
            FolderId = "folder_01HZY7Z6N7J4Q2X8Y9V0FLD001",
            WorkspaceId = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001",
            OperationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            PathMetadata = SamplePath(),
            MediaType = "text/plain",
        };

        AcceptedCommand result = await client.UploadFileAsync(
            descriptor, content, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(AcceptedCommandStatus.Accepted);

        handler.Request.ShouldNotBeNull();
        handler.Request!.RequestUri!.AbsolutePath.ShouldEndWith("/files/add");
        handler.Request.Headers.GetValues("Idempotency-Key").Single().ShouldBe("idem_01HZY7Z6N7J4Q2X8Y9V0IDK001");
        handler.Request.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr_01HZY7Z6N7J4Q2X8Y9V0COR001");
        handler.Request.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task_01HZY7Z6N7J4Q2X8Y9V0TSK001");

        JObject body = JObject.Parse(handler.RequestBody!);
        body["transportOperation"]!.Value<string>().ShouldBe("PutFileInline");
        body["byteLength"]!.Value<int>().ShouldBe(content.Length);
        body["inlineContent"]!["contentBytes"].ShouldNotBeNull();
        body.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedRequestFields);
    }

    [Fact]
    public async Task UploadFileAsyncChangeRoutesToChangeFile()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        byte[] content = Encoding.UTF8.GetBytes("synthetic");
        FileUploadDescriptor descriptor = new()
        {
            FolderId = "folder_01HZY7Z6N7J4Q2X8Y9V0FLD001",
            WorkspaceId = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001",
            OperationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            PathMetadata = SamplePath(),
            MediaType = "text/plain",
            FileOperationKind = FileMutationRequestFileOperationKind.Change,
        };

        _ = await client.UploadFileAsync(
            descriptor, content, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Request!.RequestUri!.AbsolutePath.ShouldEndWith("/files/change");
    }

    [Fact]
    public async Task UploadFileAsyncSurfacesReplayFromAcceptedCommand()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: true));
        IClient client = NewClient(handler);

        AcceptedCommand result = await client.UploadFileAsync(
            InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IdempotentReplay.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadFileAsyncSurfacesIdempotencyConflictAsTypedProblem()
    {
        const string conflictJson = """
            {"type":"about:blank","title":"Conflict","status":409,"category":"idempotency_conflict","code":"idempotency_conflict","message":"Synthetic conflict.","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","retryable":false}
            """;
        CapturingHandler handler = new(HttpStatusCode.Conflict, conflictJson);
        IClient client = NewClient(handler);

        HexalithFoldersApiException<ProblemDetails> exception = await Should.ThrowAsync<HexalithFoldersApiException<ProblemDetails>>(
            () => client.UploadFileAsync(InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken)).ConfigureAwait(true);

        exception.StatusCode.ShouldBe(409);
        exception.Result.Code.ShouldBe("idempotency_conflict");
    }

    [Fact]
    public async Task UploadFileAsyncTranslatesServer413IntoStreamingRequired()
    {
        const string payloadTooLargeJson = """
            {"type":"about:blank","title":"Payload Too Large","status":413,"category":"validation_error","code":"input_limit_exceeded","message":"Synthetic.","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","retryable":false}
            """;
        CapturingHandler handler = new(HttpStatusCode.RequestEntityTooLarge, payloadTooLargeJson);
        IClient client = NewClient(handler);

        _ = await Should.ThrowAsync<FileUploadStreamingRequiredException>(
            () => client.UploadFileAsync(InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task UploadFileAsyncFromStreamRejectsOverBoundaryWithoutCallingServer()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        using MemoryStream content = new(new byte[FileUpload.InlineTransportBoundaryBytes + 1]);

        _ = await Should.ThrowAsync<FileUploadStreamingRequiredException>(
            () => client.UploadFileAsync(InlineDescriptor(), content, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.Request.ShouldBeNull();
    }

    [Fact]
    public void BuildInlineSetsContentMediaTypeAndSupportsChangeKind()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic authorized request body");

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            content,
            "text/plain",
            SamplePath(),
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            FileMutationRequestFileOperationKind.Change,
            contentMediaType: "text/markdown");

        request.FileOperationKind.ShouldBe(FileMutationRequestFileOperationKind.Change);
        var inline = request.AdditionalProperties["inlineContent"].ShouldBeOfType<PutFileInline>();
        inline.ContentMediaType.ShouldBe("text/markdown");
    }

    [Fact]
    public void BuildInlineAcceptsEmptyContentAtZeroLength()
    {
        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            ReadOnlyMemory<byte>.Empty, "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        request.TransportOperation.ShouldBe("PutFileInline");
        request.ByteLength.ShouldBe(0);
        var inline = request.AdditionalProperties["inlineContent"].ShouldBeOfType<PutFileInline>();
        inline.ContentBytes.ShouldBe(string.Empty);
    }

    [Fact]
    public void BuildStreamedRejectsRemoveOperationKind()
    {
        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes + 1,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => FileUpload.BuildStreamedFileMutation(evidence, "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV", (FileMutationRequestFileOperationKind)2));
    }

    [Fact]
    public void BuildStreamedRejectsMissingStagingEvidenceFields()
    {
        // Streamed transport requires observed staging evidence; blank references fail closed.
        _ = Should.Throw<ArgumentException>(() => FileUpload.BuildStreamedFileMutation(
            new FileStreamStagingEvidence { StagingReference = "  ", ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002", ObservedLength = FileUpload.InlineTransportBoundaryBytes + 1 },
            "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV"));

        _ = Should.Throw<ArgumentException>(() => FileUpload.BuildStreamedFileMutation(
            new FileStreamStagingEvidence { StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001", ObservedContentHashReference = "  ", ObservedLength = FileUpload.InlineTransportBoundaryBytes + 1 },
            "application/octet-stream", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV"));
    }

    [Fact]
    public void ComputeIdempotencyKeyValidatesArguments()
    {
        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            Encoding.UTF8.GetBytes("synthetic"), "text/plain", SamplePath(), "01ARZ3NDEKTSV4RRFFQ69G5FAV");

        _ = Should.Throw<ArgumentNullException>(() => FileUpload.ComputeIdempotencyKey(null!, "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001"));
        _ = Should.Throw<ArgumentException>(() => FileUpload.ComputeIdempotencyKey(request, "  ", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001"));
        _ = Should.Throw<ArgumentException>(() => FileUpload.ComputeIdempotencyKey(request, "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001", "  "));
    }

    [Fact]
    public async Task UploadFileAsyncFromStreamUploadsInlineWhenAtOrBelowBoundary()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        byte[] payload = Encoding.UTF8.GetBytes("synthetic authorized request body from a stream");
        using MemoryStream content = new(payload);

        AcceptedCommand result = await client.UploadFileAsync(
            InlineDescriptor(), content, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(AcceptedCommandStatus.Accepted);

        JObject body = JObject.Parse(handler.RequestBody!);
        body["transportOperation"]!.Value<string>().ShouldBe("PutFileInline");
        body["byteLength"]!.Value<int>().ShouldBe(payload.Length);
        Convert.FromBase64String(body["inlineContent"]!["contentBytes"]!.Value<string>()!).ShouldBe(payload);
    }

    [Fact]
    public async Task UploadStreamedFileAsyncRoutesStreamedBodyAndHeaderTripleToAddFile()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes + 4096,
        };

        _ = await client.UploadStreamedFileAsync(
            InlineDescriptor(), evidence, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Request!.RequestUri!.AbsolutePath.ShouldEndWith("/files/add");
        handler.Request.Headers.GetValues("Idempotency-Key").Single().ShouldBe("idem_01HZY7Z6N7J4Q2X8Y9V0IDK001");
        handler.Request.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr_01HZY7Z6N7J4Q2X8Y9V0COR001");
        handler.Request.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task_01HZY7Z6N7J4Q2X8Y9V0TSK001");

        JObject body = JObject.Parse(handler.RequestBody!);
        body["transportOperation"]!.Value<string>().ShouldBe("PutFileStream");
        body["byteLength"]!.Value<int>().ShouldBe(evidence.ObservedLength);
        body["streamDescriptor"]!["stagingReference"]!.Value<string>().ShouldBe(evidence.StagingReference);
        body.Properties().Select(p => p.Name).ShouldBeSubsetOf(AllowedRequestFields);
    }

    [Fact]
    public async Task UploadStreamedFileAsyncChangeRoutesToChangeFile()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        FileUploadDescriptor descriptor = InlineDescriptor() with { FileOperationKind = FileMutationRequestFileOperationKind.Change };
        FileStreamStagingEvidence evidence = new()
        {
            StagingReference = "staging_01HZY7Z6N7J4Q2X8Y9V0STG001",
            ObservedContentHashReference = "hashref_01HZY7Z6N7J4Q2X8Y9V0CHG002",
            ObservedLength = FileUpload.InlineTransportBoundaryBytes + 4096,
        };

        _ = await client.UploadStreamedFileAsync(
            descriptor, evidence, "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Request!.RequestUri!.AbsolutePath.ShouldEndWith("/files/change");
    }

    [Fact]
    public async Task UploadFileAsyncDoesNotTranslateNon413ServerErrors()
    {
        const string serverErrorJson = """
            {"type":"about:blank","title":"Internal Server Error","status":500,"category":"infrastructure_error","code":"internal_error","message":"Synthetic.","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","retryable":true}
            """;
        CapturingHandler handler = new(HttpStatusCode.InternalServerError, serverErrorJson);
        IClient client = NewClient(handler);

        // A non-413 failure must propagate as the generated API exception, never the 413-only streaming signal.
        _ = await Should.ThrowAsync<HexalithFoldersApiException>(
            () => client.UploadFileAsync(InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task UploadFileAsync413TranslationPreservesOriginatingApiException()
    {
        const string payloadTooLargeJson = """
            {"type":"about:blank","title":"Payload Too Large","status":413,"category":"validation_error","code":"input_limit_exceeded","message":"Synthetic.","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","retryable":false}
            """;
        CapturingHandler handler = new(HttpStatusCode.RequestEntityTooLarge, payloadTooLargeJson);
        IClient client = NewClient(handler);

        FileUploadStreamingRequiredException exception = await Should.ThrowAsync<FileUploadStreamingRequiredException>(
            () => client.UploadFileAsync(InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), "idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", TestContext.Current.CancellationToken)).ConfigureAwait(true);

        // The streaming signal wraps the underlying 413 so diagnostics retain the cause without leaking limits.
        HexalithFoldersApiException inner = exception.InnerException.ShouldBeAssignableTo<HexalithFoldersApiException>()!;
        inner.StatusCode.ShouldBe(413);
        exception.Message.ShouldNotContain(FileUpload.InlineTransportBoundaryBytes.ToString());
    }

    [Theory]
    [InlineData("", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001")]
    [InlineData("idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "   ", "task_01HZY7Z6N7J4Q2X8Y9V0TSK001")]
    [InlineData("idem_01HZY7Z6N7J4Q2X8Y9V0IDK001", "corr_01HZY7Z6N7J4Q2X8Y9V0COR001", "")]
    public async Task UploadFileAsyncRejectsBlankHeaderTripleWithoutCallingServer(string idempotencyKey, string correlationId, string taskId)
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedJson(idempotentReplay: false));
        IClient client = NewClient(handler);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => client.UploadFileAsync(InlineDescriptor(), Encoding.UTF8.GetBytes("synthetic"), idempotencyKey, correlationId, taskId, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.Request.ShouldBeNull();
    }

    private static FileUploadDescriptor InlineDescriptor() => new()
    {
        FolderId = "folder_01HZY7Z6N7J4Q2X8Y9V0FLD001",
        WorkspaceId = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001",
        OperationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        PathMetadata = SamplePath(),
        MediaType = "text/plain",
    };

    private static IClient NewClient(CapturingHandler handler)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://folders.example/") };
        return new GeneratedFoldersClient(httpClient);
    }

    private static string AcceptedJson(bool idempotentReplay) =>
        $$"""
        {"acceptedAt":"2026-05-27T12:00:00+00:00","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","taskId":"task_01HZY7Z6N7J4Q2X8Y9V0TSK001","status":"accepted","idempotentReplay":{{(idempotentReplay ? "true" : "false")}}}
        """;

    private sealed class CapturingHandler(HttpStatusCode statusCode, string responseJson) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }
}
