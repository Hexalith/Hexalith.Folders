using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies <c>add-file</c> goes through the Story 5.1 upload convenience: in-boundary content selects the
/// inline transport internally, and over-boundary content surfaces the content-safe
/// <c>input_limit_exceeded</c> failure without any HTTP call. File content never appears in tool output.
/// </summary>
public sealed class FileTransportTests
{
    [Fact]
    public async Task AddFileSelectsInlineTransportAndNeverEchoesContent()
    {
        const string content = "hello-file-content";
        string contentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));

        FileMutationRequest? captured = null;
        IClient client = Substitute.For<IClient>();
        client.AddFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Do<FileMutationRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AcceptedCommand { Status = AcceptedCommandStatus.Accepted }));
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FileTools.AddFile(
            pipeline,
            folderId: "f",
            workspaceId: "w",
            operationId: "op-1",
            path: "docs/readme.md",
            displayName: "readme.md",
            mediaType: "text/markdown",
            contentBase64: contentBase64,
            idempotencyKey: "idem-1",
            taskId: "task-1",
            correlationId: "corr-add",
            cancellationToken: TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.TransportOperation.ShouldBe("PutFileInline");
        result.ShouldNotContain(content);
        result.ShouldNotContain(contentBase64);
        TestSupport.Kind(result).ShouldBeNull(); // success envelope, not a failure
    }

    [Fact]
    public async Task AddFileOverInlineBoundaryIsInputLimitExceededWithNoCall()
    {
        byte[] oversized = new byte[FileUpload.InlineTransportBoundaryBytes + 1];
        string contentBase64 = Convert.ToBase64String(oversized);

        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FileTools.AddFile(
            pipeline,
            folderId: "f",
            workspaceId: "w",
            operationId: "op-1",
            path: "docs/big.bin",
            displayName: "big.bin",
            mediaType: "application/octet-stream",
            contentBase64: contentBase64,
            idempotencyKey: "idem-1",
            taskId: "task-1",
            correlationId: "corr-big",
            cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("input_limit_exceeded");
        client.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IClient.AddFileAsync)).ShouldBeFalse();
    }

    [Fact]
    public async Task AddFileWithInvalidBase64IsUsageErrorWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FileTools.AddFile(
            pipeline,
            folderId: "f",
            workspaceId: "w",
            operationId: "op-1",
            path: "docs/x.md",
            displayName: "x.md",
            mediaType: "text/markdown",
            contentBase64: "not valid base64 !!!",
            idempotencyKey: "idem-1",
            taskId: "task-1",
            correlationId: "corr-bad",
            cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        client.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IClient.AddFileAsync)).ShouldBeFalse();
    }
}
