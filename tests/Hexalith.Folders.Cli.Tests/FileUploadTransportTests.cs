using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;

using NSubstitute;

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
        handler.RequestBody.ShouldContain("\"pathPolicyClass\":\"metadata_only\"");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    public async Task InvalidPolicyClassUsesStableUsageErrorBeforeSdk(string policyClass)
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
            "--path-policy-class", policyClass,
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1");

        exit.ShouldBe(64);
        handler.Request.ShouldBeNull();
        harness.Console.StdErr.ShouldContain("client_configuration_error");
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
        harness.Console.StdErr.ShouldContain("d9_inline_limit_exceeded");
        harness.Console.StdErr.ShouldContain("retryable: true");
        harness.Console.StdErr.ShouldContain("clientAction: revise_request");
    }

    [Fact]
    public async Task OverAbsoluteLimitUsesCanonical422WithoutCallingSdk()
    {
        System.IO.File.WriteAllBytes(_contentPath, new byte[FileUpload.MaximumFileBytes + 1]);
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await RunAddAsync(harness, "docs/too-big.bin", "too-big.bin");

        exit.ShouldBe(69);
        handler.Request.ShouldBeNull();
        harness.Console.StdErr.ShouldContain("file_content_limit_exceeded");
        harness.Console.StdErr.ShouldContain("retryable: false");
        harness.Console.StdErr.ShouldContain("clientAction: revise_request");
    }

    [Fact]
    public async Task CallbackWrappedRequestValidationUsesStableUsageError()
    {
        System.IO.File.WriteAllText(_contentPath, "small synthetic authorized content");
        IClient client = Substitute.For<IClient>();
        client.AddFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AddFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AcceptedCommand>(new TargetInvocationException(new JsonSerializationException("synthetic validation"))));
        CliTestHarness harness = new() { Client = client };

        int exit = await RunAddAsync(harness, "docs/readme.md", "readme.md");

        exit.ShouldBe(64);
        harness.Console.StdErr.ShouldContain("client_configuration_error");
        harness.Console.StdErr.ShouldNotContain("synthetic validation");
    }

    [Fact]
    public async Task RemoveRequiresBodyBeforeSdkCall()
    {
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "file", "remove",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1");

        exit.ShouldBe(64);
        handler.Request.ShouldBeNull();
    }

    private async Task<int> RunAddAsync(CliTestHarness harness, string path, string displayName) => await harness.RunAsync(
        "file", "add",
        "--folder-id", "folder_1",
        "--workspace-id", "workspace_1",
        "--operation-id", "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        "--file", _contentPath,
        "--path", path,
        "--display-name", displayName,
        "--media-type", "application/octet-stream",
        "--base-address", BaseAddress,
        "--token", Token,
        "--task-id", "task_1",
        "--idempotency-key", "key_1").ConfigureAwait(false);

    public void Dispose()
    {
        if (System.IO.File.Exists(_contentPath))
        {
            System.IO.File.Delete(_contentPath);
        }
    }
}
