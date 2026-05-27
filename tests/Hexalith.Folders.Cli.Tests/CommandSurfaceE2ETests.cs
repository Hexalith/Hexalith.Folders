using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// End-to-end command-surface coverage proving the CLI exposes and wires all seven canonical groups
/// (AC #2) and that the golden lifecycle path executes through the CLI as a thin adapter over the SDK.
/// Commands are driven through <c>rootCommand.Parse(args).InvokeAsync()</c> against a fake <see cref="IClient"/>
/// (NSubstitute) — no server, Dapr, Keycloak, Redis, or network. A reachable command returns exit 0 after a
/// single SDK call; the golden path asserts each lifecycle step succeeds and the overridden correlation ID is
/// propagated unchanged across every invocation.
/// </summary>
public sealed class CommandSurfaceE2ETests : IDisposable
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";

    private readonly string _contentPath = Path.Combine(Path.GetTempPath(), $"hexalith-golden-{Guid.NewGuid():N}.txt");

    /// <summary>One representative reachable command per canonical group, with its complete argument vector.</summary>
    public static TheoryData<string, string[]> ReachableCommands() => new()
    {
        { "provider", ["provider", "get-binding", "--provider-binding-ref", "pbr_1", "--base-address", BaseAddress, "--token", Token] },
        { "folder", ["folder", "status", "--folder-id", "folder_1", "--base-address", BaseAddress, "--token", Token] },
        { "workspace", ["workspace", "status", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--base-address", BaseAddress, "--token", Token] },
        { "file", ["file", "remove", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--idempotency-key", "key_1", "--request", "{}", "--base-address", BaseAddress, "--token", Token] },
        { "commit", ["commit", "reconciliation-status", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--reconciliation-id", "recon_1", "--base-address", BaseAddress, "--token", Token] },
        { "context", ["context", "list", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--base-address", BaseAddress, "--token", Token] },
        { "audit", ["audit", "list", "--folder-id", "folder_1", "--base-address", BaseAddress, "--token", Token] },
    };

    [Theory]
    [MemberData(nameof(ReachableCommands))]
    public async Task EveryCanonicalGroupHasAReachableCommandThatWrapsTheSdk(string group, string[] args)
    {
        IClient client = Substitute.For<IClient>();
        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(args);

        exit.ShouldBe(0, $"the '{group}' group should expose a command that reaches the SDK and succeeds");
        harness.ClientFactoryInvoked.ShouldBeTrue($"the '{group}' command should build and call the SDK client");
    }

    [Fact]
    public async Task GoldenLifecyclePathExecutesEndToEndOverTheSdk()
    {
        const string correlation = "corr_GOLDEN_PATH_TRACE_00000001";
        File.WriteAllText(_contentPath, "small synthetic authorized content");

        IClient client = Substitute.For<IClient>();

        // Configure the accepted-command mutations to acknowledge truthfully; queries default to null (exit 0).
        client.CreateRepositoryBackedFolderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CreateRepositoryBackedFolderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestData.Accepted()));
        client.AddFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileMutationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestData.Accepted()));

        // configure provider binding → validate readiness → create repo-backed folder → prepare → lock →
        // add file → commit → query context → release lock → inspect audit.
        await RunStepAsync(client, correlation, "provider", "configure-binding", "--provider-binding-ref", "pbr_1", "--task-id", "task_1", "--idempotency-key", "key_provider", "--request", "{}");
        await RunStepAsync(client, correlation, "provider", "validate-readiness", "--request", "{}");
        await RunStepAsync(client, correlation, "folder", "create-repo-backed", "--task-id", "task_1", "--idempotency-key", "key_folder", "--request", "{}");
        await RunStepAsync(client, correlation, "workspace", "prepare", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--idempotency-key", "key_prepare", "--request", "{}");
        await RunStepAsync(client, correlation, "workspace", "lock", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--idempotency-key", "key_lock", "--request", "{}");
        await RunStepAsync(client, correlation, "file", "add", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--operation-id", "01ARZ3NDEKTSV4RRFFQ69G5FAV", "--file", _contentPath, "--path", "docs/readme.md", "--display-name", "readme.md", "--media-type", "text/plain", "--task-id", "task_1", "--idempotency-key", "key_file");
        await RunStepAsync(client, correlation, "commit", "create", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--idempotency-key", "key_commit", "--request", "{}");
        await RunStepAsync(client, correlation, "context", "list", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1");
        await RunStepAsync(client, correlation, "workspace", "release", "--folder-id", "folder_1", "--workspace-id", "workspace_1", "--task-id", "task_1", "--idempotency-key", "key_release", "--request", "{}");
        await RunStepAsync(client, correlation, "audit", "list", "--folder-id", "folder_1");

        // The golden path's mutations and queries each reached their canonical SDK operation exactly once.
        await client.Received(1).CreateRepositoryBackedFolderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CreateRepositoryBackedFolderRequest>(), Arg.Any<CancellationToken>());
        await client.Received(1).AddFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileMutationRequest>(), Arg.Any<CancellationToken>());
        await client.Received(1).CommitWorkspaceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommitWorkspaceRequest>(), Arg.Any<CancellationToken>());
        await client.Received(1).ReleaseWorkspaceLockAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReleaseWorkspaceLockRequest>(), Arg.Any<CancellationToken>());
        await client.Received(1).ListAuditTrailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileChangeRoutesThroughTheUploadConvenience()
    {
        File.WriteAllText(_contentPath, "synthetic changed content");
        IClient client = Substitute.For<IClient>();
        client.ChangeFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileMutationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestData.Accepted()));
        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "file", "change",
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
        await client.Received(1).ChangeFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileMutationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitGroupSubcommandParsesAndWrapsTheSdk()
    {
        // Regression: the "commit" group previously declared a child also named "commit", which collided in
        // the System.CommandLine token table (group name == child name) and threw at parse time for EVERY
        // `commit <subcommand>` invocation. The child is now "create"; the whole group must parse cleanly.
        IClient client = Substitute.For<IClient>();
        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "commit", "create",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}",
            "--base-address", BaseAddress,
            "--token", Token);

        exit.ShouldBe(0);
        await client.Received(1).CommitWorkspaceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommitWorkspaceRequest>(), Arg.Any<CancellationToken>());
    }

    private static async Task RunStepAsync(IClient client, string correlation, params string[] commandArgs)
    {
        // A fresh harness per step (own console) sharing the same fake client so Received() counts accumulate.
        CliTestHarness harness = new() { Client = client };
        string[] args = new string[commandArgs.Length + 6];
        commandArgs.CopyTo(args, 0);
        args[commandArgs.Length] = "--base-address";
        args[commandArgs.Length + 1] = BaseAddress;
        args[commandArgs.Length + 2] = "--token";
        args[commandArgs.Length + 3] = Token;
        args[commandArgs.Length + 4] = "--correlation-id";
        args[commandArgs.Length + 5] = correlation;

        int exit = await harness.RunAsync(args).ConfigureAwait(false);

        exit.ShouldBe(0);
        // The overridden correlation ID is propagated unchanged and observable on every invocation.
        harness.Console.StdErr.ShouldContain($"correlation-id: {correlation}");
    }

    public void Dispose()
    {
        if (File.Exists(_contentPath))
        {
            File.Delete(_contentPath);
        }
    }
}
