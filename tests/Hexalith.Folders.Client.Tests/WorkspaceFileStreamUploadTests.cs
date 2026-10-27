using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class WorkspaceFileStreamUploadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(262144)]
    [InlineData(262145)]
    [InlineData(1048576)]
    public async Task ActualBytesHeadersAndFencesArePreserved(int length)
    {
        byte[] bytes = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
        var handler = new InspectingHandler(async request =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/folders/folder-a/workspaces/workspace-a/files/add");
            request.Headers.GetValues("Idempotency-Key").Single().ShouldBe("key-a");
            request.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            request.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");
            string metadata;
            if (length <= 262144) metadata = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            else
            {
                var parts = ((MultipartFormDataContent)request.Content!).ToArray();
                parts.Length.ShouldBe(2);
                parts[0].Headers.ContentDisposition!.Name.ShouldBe("metadata");
                parts[1].Headers.ContentDisposition!.Name.ShouldBe("content");
                metadata = await parts[0].ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
                (await parts[1].ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(bytes);
            }
            using JsonDocument document = JsonDocument.Parse(metadata);
            var root = document.RootElement;
            root.GetProperty("expectedLockFence").GetInt64().ShouldBe(9);
            root.GetProperty("expectedFileVersion").GetInt64().ShouldBe(0);
            root.GetProperty("contentHashReference").GetString().ShouldBe("hashref_" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
            root.GetProperty("byteLength").GetInt64().ShouldBe(length);
            root.TryGetProperty("ownerFileId", out _).ShouldBeFalse();
            if (length <= 262144)
            {
                root.TryGetProperty("streamDescriptor", out _).ShouldBeFalse();
                Convert.FromBase64String(root.GetProperty("inlineContent").GetProperty("contentBytes").GetString()!).ShouldBe(bytes);
            }
            else root.TryGetProperty("inlineContent", out _).ShouldBeFalse();
        });
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        using var stream = new MemoryStream(bytes);
        using HttpResponseMessage response = await client.UploadWorkspaceFileAsync(Descriptor(), stream, "key-a", "correlation-a", "task-a", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        handler.Calls.ShouldBe(1); stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task OversizeAndHashMismatchFailBeforeSendingAndLeaveCallerStreamOpen()
    {
        var handler = new InspectingHandler(_ => Task.CompletedTask);
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        using var oversized = new MemoryStream(new byte[1048577]);
        await Should.ThrowAsync<FileContentLimitExceededException>(() => client.UploadWorkspaceFileAsync(Descriptor(), oversized, "key", "correlation", "task", TestContext.Current.CancellationToken));
        using var mismatch = new MemoryStream(Encoding.UTF8.GetBytes("exact bytes"));
        await Should.ThrowAsync<ArgumentException>(() => client.UploadWorkspaceFileAsync(Descriptor() with { ContentHashReference = "hashref_wrong" }, mismatch, "key", "correlation", "task", TestContext.Current.CancellationToken));
        handler.Calls.ShouldBe(0); oversized.CanRead.ShouldBeTrue(); mismatch.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task CancellationStopsBeforeSendingWithoutDisposingCallerStream()
    {
        var handler = new InspectingHandler(_ => Task.CompletedTask);
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        using var stream = new MemoryStream(new byte[32]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => client.UploadWorkspaceFileAsync(Descriptor(), stream, "key", "correlation", "task", cancellation.Token));
        handler.Calls.ShouldBe(0); stream.CanRead.ShouldBeTrue();
    }

    private static FileUploadDescriptor Descriptor() => new()
    {
        FolderId = "folder-a",
        WorkspaceId = "workspace-a",
        OperationId = "operation-a",
        MediaType = "application/octet-stream",
        ExpectedLockFence = 9,
        ExpectedFileVersion = 0,
        PathMetadata = new() { NormalizedPath = "docs/a.txt", DisplayName = "a.txt", PathPolicyClass = PathMetadataPathPolicyClass.Content_allowed },
    };

    private sealed class InspectingHandler(Func<HttpRequestMessage, Task> inspect) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await inspect(request).ConfigureAwait(false);
            return new(HttpStatusCode.Accepted);
        }
    }
}
