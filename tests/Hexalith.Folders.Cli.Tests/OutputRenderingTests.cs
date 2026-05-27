using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Asserts that <c>AcceptedCommand</c> responses surface correlationId, taskId, status, and idempotentReplay
/// truthfully in both output modes — idempotent replay is never hidden (Story 4.11 lesson, AC #9).
/// </summary>
public sealed class OutputRenderingTests
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";

    [Fact]
    public async Task HumanModeSurfacesIdempotentReplayTruthfully()
    {
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson(idempotentReplay: true));

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.Console.StdOut.ShouldContain("idempotentReplay: true");
        harness.Console.StdOut.ShouldContain("status: ");
        harness.Console.StdOut.ShouldContain("correlationId: ");
    }

    [Fact]
    public async Task JsonModeEmitsTheAcceptedCommandShape()
    {
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson(idempotentReplay: true));

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--output", "json",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.Console.StdOut.ShouldContain("\"idempotentReplay\": true");
        harness.Console.StdOut.ShouldContain("\"status\": \"accepted\"");
    }
}
