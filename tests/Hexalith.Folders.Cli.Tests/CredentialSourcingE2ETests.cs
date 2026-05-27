using System.IO;
using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// End-to-end credential-sourcing assertions that drive the full CLI (not just the resolver unit) and prove
/// the Adapter Parity Contract token precedence resolves through the whole invocation: the resolved token is
/// the one attached to the client and a call is actually made. Each layer is exercised hermetically with an
/// injected environment and an injected credentials-file path, so neither <c>~/.hexalith</c> nor the process
/// environment is ever read, and a missing token still fails closed (exit 65) before any call.
/// </summary>
public sealed class CredentialSourcingE2ETests
{
    private const string BaseAddress = "https://folders.test/";

    [Fact]
    public async Task EnvironmentTokenResolvesThroughTheCliAndIsAttachedToTheClient()
    {
        CliTestHarness harness = new();
        harness.SetEnvironment("HEXALITH_TOKEN", "env-token");
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        // No --token flag: the env layer must win and the call must proceed.
        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.ClientFactoryInvoked.ShouldBeTrue();
        harness.CapturedToken.ShouldBe("env-token");
    }

    [Fact]
    public async Task CredentialsFileTokenResolvesThroughTheCliWhenNoEnvironmentToken()
    {
        CliTestHarness harness = new();
        harness.WriteCredentialsFile("default", "file-token");
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        // No env token and no --token flag: the credentials-file layer must win.
        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.ClientFactoryInvoked.ShouldBeTrue();
        harness.CapturedToken.ShouldBe("file-token");

        if (File.Exists(harness.CredentialsFilePath))
        {
            File.Delete(harness.CredentialsFilePath);
        }
    }

    [Fact]
    public async Task FlagTokenResolvesThroughTheCliWhenNoEnvironmentOrFileToken()
    {
        CliTestHarness harness = new();
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", "flag-token",
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.ClientFactoryInvoked.ShouldBeTrue();
        harness.CapturedToken.ShouldBe("flag-token");
    }

    [Fact]
    public async Task EnvironmentTokenWinsOverFlagTokenThroughTheCli()
    {
        CliTestHarness harness = new();
        harness.SetEnvironment("HEXALITH_TOKEN", "env-token");
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", "flag-token",
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.CapturedToken.ShouldBe("env-token");
    }
}
