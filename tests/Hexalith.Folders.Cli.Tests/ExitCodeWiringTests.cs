using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Proves the post-SDK path is wired to the canonical projection: a typed
/// <see cref="HexalithFoldersApiException{ProblemDetails}"/> from the SDK becomes the projected exit code,
/// and a bare (unmapped) exception becomes exit 1 with the correlation ID emitted to stderr.
/// </summary>
public sealed class ExitCodeWiringTests
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> NoHeaders = new Dictionary<string, IEnumerable<string>>();

    [Theory]
    [InlineData(CanonicalErrorCategory.Not_found, 73)]
    [InlineData(CanonicalErrorCategory.Workspace_locked, 67)]
    [InlineData(CanonicalErrorCategory.Validation_error, 69)]
    [InlineData(CanonicalErrorCategory.Unknown_provider_outcome, 71)]
    [InlineData(CanonicalErrorCategory.Reconciliation_required, 72)]
    [InlineData(CanonicalErrorCategory.Tenant_access_denied, 66)]
    [InlineData(CanonicalErrorCategory.Idempotency_conflict, 68)]
    [InlineData(CanonicalErrorCategory.Provider_unavailable, 70)]
    [InlineData(CanonicalErrorCategory.State_transition_invalid, 74)]
    [InlineData(CanonicalErrorCategory.Redacted, 75)]
    [InlineData(CanonicalErrorCategory.Authentication_failure, 65)]
    public async Task TypedProblemProjectsToCanonicalExitCode(CanonicalErrorCategory category, int expectedExit)
    {
        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<FolderLifecycleStatus>(TestData.ProblemException(category)));

        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", Token);

        exit.ShouldBe(expectedExit);
        harness.Console.StdErr.ShouldContain(category.ToString());
    }

    [Fact]
    public async Task TypedProblemInJsonModeEmitsProjectedShapeOnStdErr()
    {
        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<FolderLifecycleStatus>(TestData.ProblemException(CanonicalErrorCategory.Validation_error)));

        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", Token,
            "--output", "json");

        exit.ShouldBe(69);
        // Projected from typed fields only, emitted in camelCase to match the wire/SDK ProblemDetails shape.
        harness.Console.StdErr.ShouldContain("\"category\": \"Validation_error\"");
        harness.Console.StdErr.ShouldContain("\"code\": \"test_code\"");
        harness.Console.StdErr.ShouldContain("\"correlationId\": \"corr_TEST\"");
        harness.Console.StdOut.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("file_policy_unavailable", 72)]
    [InlineData("range_unsatisfiable", 69)]
    public async Task ExactFileProblemDtosPreserveCanonicalProjection(string category, int expectedExit)
    {
        string response = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = "File request failed",
            status = 503,
            category,
            code = category,
            message = "The file request could not be completed.",
            correlationId = "server-file-correlation",
            retryable = true,
            clientAction = "retry",
            details = new { visibility = "redacted" },
        });
        HexalithFoldersApiException exact = category == "file_policy_unavailable"
            ? new HexalithFoldersApiException<FileMutationUnavailableProblem>("policy", 503, response, NoHeaders, null!, null)
            : new HexalithFoldersApiException<FileRangeUnsatisfiableProblem>("range", 416, response, NoHeaders, null!, null);

        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<FolderLifecycleStatus>(exact));
        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", Token);

        exit.ShouldBe(expectedExit);
        harness.Console.StdErr.ShouldContain(category);
        harness.Console.StdErr.ShouldContain("server-file-correlation");
    }

    [Fact]
    public async Task BareExceptionIsInternalErrorWithCorrelationEmitted()
    {
        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<FolderLifecycleStatus>(TestData.BareException()));

        CliTestHarness harness = new() { Client = client };

        int exit = await harness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", BaseAddress,
            "--token", Token);

        exit.ShouldBe(1);
        harness.Console.StdErr.ShouldContain("internal_error");
        harness.Console.StdErr.ShouldContain("correlation-id:");
    }
}
