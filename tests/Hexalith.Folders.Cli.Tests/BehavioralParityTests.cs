using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Hermetic behavioral-parity assertions for the Adapter Parity Contract sourcing rules: idempotency-key,
/// correlation-ID, task-ID, and credential sourcing, plus the pre-SDK exit codes (64/65). No live server,
/// Dapr, Keycloak, Redis, or network — pre-SDK failures never even build a client.
/// </summary>
public sealed class BehavioralParityTests
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";

    [Fact]
    public async Task MutatingCommandWithoutIdempotencyKeyIsUsageErrorAndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--request", "{}");

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
        harness.Console.StdErr.ShouldContain("client_configuration_error");
    }

    [Fact]
    public async Task AllowAutoKeyPrintsGeneratedKeyToStderrAndProceeds()
    {
        CliTestHarness harness = new() { IdempotencyKeyGenerator = () => "01AUTOKEYTESTVALUE0000000AB" };
        _ = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--allow-auto-key",
            "--request", "{}");

        exit.ShouldBe(0);
        harness.Console.StdErr.ShouldContain("idempotency-key: 01AUTOKEYTESTVALUE0000000AB");
    }

    [Fact]
    public async Task AllowAutoKeyPropagatesGeneratedKeyToTheWireHeader()
    {
        CliTestHarness harness = new() { IdempotencyKeyGenerator = () => "01AUTOKEYTESTVALUE0000000AB" };
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        _ = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--allow-auto-key",
            "--request", "{}");

        handler.Header("Idempotency-Key").ShouldBe("01AUTOKEYTESTVALUE0000000AB");
    }

    [Fact]
    public async Task QueryCommandRejectsIdempotencyKeyOption()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", Token,
            "--idempotency-key", "k");

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task MutatingCommandWithoutTaskIdIsUsageErrorAndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task TaskScopedQueryWithoutTaskIdIsUsageErrorAndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "context", "list",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--base-address", BaseAddress,
            "--token", Token);

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task MutatingCommandWithNonAbsoluteBaseAddressIsUsageErrorAndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", "not-an-absolute-uri",
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
        harness.Console.StdErr.ShouldContain("client_configuration_error");
    }

    [Fact]
    public async Task QueryCommandWithNonAbsoluteBaseAddressIsUsageErrorAndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", "not-an-absolute-uri",
            "--token", Token);

        exit.ShouldBe(64);
        harness.ClientFactoryInvoked.ShouldBeFalse();
        harness.Console.StdErr.ShouldContain("client_configuration_error");
    }

    [Fact]
    public async Task MissingCredentialIsExit65AndMakesNoCall()
    {
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        exit.ShouldBe(65);
        harness.ClientFactoryInvoked.ShouldBeFalse();
        harness.Console.StdErr.ShouldContain("credential_missing");
    }

    [Fact]
    public async Task ExplicitCorrelationIdIsEchoedUnchangedToTheWireHeader()
    {
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "corr_FIXED_0123456789ABCDEF",
            "--request", "{}");

        exit.ShouldBe(0);
        handler.Header("X-Correlation-Id").ShouldBe("corr_FIXED_0123456789ABCDEF");
        harness.Console.StdErr.ShouldContain("correlation-id: corr_FIXED_0123456789ABCDEF");
    }

    [Fact]
    public async Task DefaultCorrelationIdIsAFreshUlidEmittedToStderr()
    {
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        _ = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        string? wireCorrelation = handler.Header("X-Correlation-Id");
        wireCorrelation.ShouldNotBeNullOrWhiteSpace();
        wireCorrelation!.Length.ShouldBe(26); // ULID shape
        harness.Console.StdErr.ShouldContain($"correlation-id: {wireCorrelation}");
    }

    [Fact]
    public async Task ExplicitIdempotencyKeyIsPropagatedAndNoAutoKeyEmitted()
    {
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress,
            "--token", Token,
            "--task-id", "task_1",
            "--idempotency-key", "key_EXPLICIT_123",
            "--request", "{}");

        exit.ShouldBe(0);
        handler.Header("Idempotency-Key").ShouldBe("key_EXPLICIT_123");
        harness.Console.StdErr.ShouldNotContain("idempotency-key:");
    }
}
