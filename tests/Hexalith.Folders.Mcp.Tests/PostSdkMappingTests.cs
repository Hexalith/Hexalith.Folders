using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies post-SDK failure mapping: a typed <see cref="HexalithFoldersApiException{ProblemDetails}"/> is
/// projected to the canonical kind and carries <c>code</c>/<c>correlationId</c>/<c>retryable</c>/
/// <c>clientAction</c> verbatim; a bare exception (null typed body) maps to <c>internal_error</c> with the
/// correlation ID always present.
/// </summary>
public sealed class PostSdkMappingTests
{
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> NoHeaders = new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public async Task TypedProblemIsProjectedWithCanonicalFields()
    {
        ProblemDetails problem = new()
        {
            Category = CanonicalErrorCategory.Workspace_locked,
            Code = "workspace_locked",
            Message = "The workspace is locked by another task.",
            CorrelationId = "server-correlation-9",
            Retryable = true,
            ClientAction = ProblemDetailsClientAction.Retry,
        };
        HexalithFoldersApiException<ProblemDetails> exception = new("locked", 409, "{}", NoHeaders, problem, null);

        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FolderLifecycleStatus>(exception));
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "client-correlation-1", cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject o = TestSupport.Parse(result);
        o.Value<string>("kind").ShouldBe("workspace_locked");
        o.Value<string>("code").ShouldBe("workspace_locked");
        o.Value<bool>("retryable").ShouldBeTrue();
        o.Value<string>("clientAction").ShouldBe("retry");
        o.Value<string>("correlationId").ShouldBe("server-correlation-9");
    }

    [Fact]
    public async Task UnknownProviderOutcomeIsSurfacedTruthfully()
    {
        ProblemDetails problem = new()
        {
            Category = CanonicalErrorCategory.Unknown_provider_outcome,
            Code = "unknown_provider_outcome",
            CorrelationId = "corr-x",
            Retryable = false,
            ClientAction = ProblemDetailsClientAction.Wait_for_reconciliation,
        };
        HexalithFoldersApiException<ProblemDetails> exception = new("unknown", 503, "{}", NoHeaders, problem, null);

        IClient client = Substitute.For<IClient>();
        client.GetProviderOutcomeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProviderOutcome>(exception));
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await CommitTools.GetProviderOutcome(
            pipeline, folderId: "f", workspaceId: "w", operationId: "op", correlationId: "corr-x", cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("unknown_provider_outcome");
    }

    [Fact]
    public async Task BareApiExceptionIsInternalErrorWithCorrelation()
    {
        HexalithFoldersApiException exception = new("unexpected", 500, "not a problem document", NoHeaders, null);

        IClient client = Substitute.For<IClient>();
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FolderLifecycleStatus>(exception));
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "corr-internal", cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject o = TestSupport.Parse(result);
        o.Value<string>("kind").ShouldBe("internal_error");
        o.Value<string>("correlationId").ShouldBe("corr-internal");
    }
}
