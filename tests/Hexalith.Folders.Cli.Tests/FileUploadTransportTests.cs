using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Convenience;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Asserts that <c>file add</c> routes through the Story 5.1 upload convenience: small content selects the
/// inline transport, and over-boundary content surfaces the content-safe streaming-required outcome (exit 69)
/// without ever leaking file bytes.
/// </summary>
public sealed class FileUploadTransportTests : IDisposable
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";

    private readonly string _contentPath = Path.Combine(Path.GetTempPath(), $"hexalith-upload-{Guid.NewGuid():N}.bin");

    [Fact]
    public async Task SmallFileAddSelectsInlineTransport()
    {
        System.IO.File.WriteAllText(_contentPath, "small synthetic authorized content");
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "file", "add",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--operation-id", "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "--file", _contentPath,
            "--path", "docs/readme.md",
            "--display-name", "readme.md",
            "--media-type", "text/plain",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1");

        exit.ShouldBe(0);
        handler.RequestBody.ShouldNotBeNull();
        handler.RequestBody!.ShouldContain("PutFileInline");
    }

    [Fact]
    public async Task OverBoundaryFileAddIsContentSafeStreamingOutcome()
    {
        byte[] tooLarge = new byte[FileUpload.InlineTransportBoundaryBytes + 1];
        System.IO.File.WriteAllBytes(_contentPath, tooLarge);
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "file", "add",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--operation-id", "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "--file", _contentPath,
            "--path", "docs/big.bin",
            "--display-name", "big.bin",
            "--media-type", "application/octet-stream",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1");

        exit.ShouldBe(69);
        harness.Console.StdErr.ShouldContain("input_limit_exceeded");
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_contentPath))
        {
            System.IO.File.Delete(_contentPath);
        }
    }
}
