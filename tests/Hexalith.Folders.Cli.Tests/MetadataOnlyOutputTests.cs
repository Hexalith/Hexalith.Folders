using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Asserts the metadata-only invariant on CLI output: authorized file content carried by a range-read
/// result is never printed, and the bearer token never appears in stdout or stderr.
/// </summary>
public sealed class MetadataOnlyOutputTests
{
    private const string BaseAddress = "https://folders.test/";

    // base64 of "SECRETBYTES" — must never reach any output channel.
    private const string ContentBase64 = "U0VDUkVUQllURVM=";

    private const string RangeReadJson =
        "{\"path\":{\"normalizedPath\":\"docs/readme.md\",\"displayName\":\"readme.md\",\"pathPolicyClass\":\"metadata_only\",\"unicodeNormalization\":\"NFC\"},"
        + "\"range\":{\"startOffset\":0,\"endOffset\":11,\"actualBytes\":11,\"partial\":false},"
        + "\"contentBytes\":\"" + ContentBase64 + "\","
        + "\"freshness\":{\"readConsistency\":\"read_your_writes\"}}";

    [Fact]
    public async Task RangeReadOutputNeverContainsFileContent()
    {
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.OK, RangeReadJson);

        int exit = await harness.RunAsync(
            "context", "read-range",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--task-id", "task_1",
            "--base-address", BaseAddress,
            "--token", "synthetic-jwt",
            "--output", "json",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.Console.StdOut.ShouldNotContain(ContentBase64);
        harness.Console.StdOut.ShouldNotContain("SECRETBYTES");
        harness.Console.StdOut.ShouldNotContain("contentBytes");
        // The metadata is still rendered (proves we serialized the result, just without content).
        harness.Console.StdOut.ShouldContain("freshness");
    }

    [Fact]
    public async Task TokenNeverAppearsInOutput()
    {
        const string secretToken = "super-secret-jwt-VALUE-do-not-leak";
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", secretToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.Console.StdOut.ShouldNotContain(secretToken);
        harness.Console.StdErr.ShouldNotContain(secretToken);
    }

    [Theory]
    [InlineData("human")]
    [InlineData("json")]
    public async Task ProblemRawResponseBodyIsNeverEchoed(string output)
    {
        // The server's raw RFC 9457 body carries a content-bearing marker that must never reach any channel;
        // the CLI projects only the typed ProblemDetails fields, never the exception's raw Response text.
        const string rawBodyLeak = "RAW-RESPONSE-LEAK-MARKER-DO-NOT-PRINT";
        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<FolderLifecycleStatus>(
                TestData.ProblemException(
                    CanonicalErrorCategory.Validation_error,
                    rawResponse: "{\"leak\":\"" + rawBodyLeak + "\"}")));

        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", "synthetic-jwt",
            "--output", output);

        exit.ShouldBe(69);
        harness.Console.StdOut.ShouldNotContain(rawBodyLeak);
        harness.Console.StdErr.ShouldNotContain(rawBodyLeak);
        // The projected (typed) category is still surfaced, proving we rendered the problem, just not the raw body.
        harness.Console.StdErr.ShouldContain("Validation_error");
    }
}
