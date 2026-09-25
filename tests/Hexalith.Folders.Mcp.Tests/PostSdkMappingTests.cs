using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using NSubstitute;
using Newtonsoft.Json;

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
    public async Task TaskStatusAndFolderDiagnosticToolsPassExactRouteArguments()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        _ = await CommitTools.GetTaskStatus(
            pipeline,
            folderId: "folder_1",
            taskId: "task_1",
            correlationId: "correlation-task-status-mcp",
            freshness: "eventually_consistent",
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await DiagnosticsTools.GetReadinessDiagnostics(
            pipeline,
            folderId: "folder_2",
            correlationId: "correlation-readiness-mcp",
            freshness: "eventually_consistent",
            cancellationToken: TestContext.Current.CancellationToken);

        await client.Received(1).GetTaskStatusAsync(
            "folder_1",
            "task_1",
            "correlation-task-status-mcp",
            ReadConsistencyClass.Eventually_consistent,
            Arg.Any<CancellationToken>());
        await client.Received(1).GetReadinessDiagnosticsAsync(
            "folder_2",
            "correlation-readiness-mcp",
            ReadConsistencyClass.Eventually_consistent,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TypedProblemIsProjectedWithCanonicalFields()
    {
        ProblemDetails problem = new()
        {
            Type = "about:blank",
            Title = "Read model unavailable",
            Status = 503,
            Category = CanonicalErrorCategory.Read_model_unavailable,
            Code = CanonicalErrorCode.Projection_unavailable,
            Message = "Projection data is temporarily unavailable.",
            CorrelationId = "server-correlation-9",
            Retryable = true,
            ClientAction = ProblemDetailsClientAction.Retry,
            Details = new Details { Visibility = DetailsVisibility.Metadata_only },
        };
        TestSupport.CapturingHandler handler = new(HttpStatusCode.ServiceUnavailable, JsonConvert.SerializeObject(problem));
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "client-correlation-1", cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject o = TestSupport.Parse(result);
        o.Value<string>("kind").ShouldBe("read_model_unavailable");
        o.Value<string>("code").ShouldBe("projection_unavailable");
        o.Value<bool>("retryable").ShouldBeTrue();
        o.Value<string>("clientAction").ShouldBe("retry");
        o.Value<string>("correlationId").ShouldBe("server-correlation-9");
    }

    [Fact]
    public async Task UnknownProviderOutcomeIsSurfacedTruthfully()
    {
        ProblemDetails problem = new()
        {
            Type = "about:blank",
            Title = "Unknown provider outcome",
            Status = 503,
            Category = CanonicalErrorCategory.Unknown_provider_outcome,
            Code = CanonicalErrorCode.Unknown_provider_outcome,
            Message = "Provider outcome is unknown for the requested workspace operation.",
            CorrelationId = "correlation-provider-unknown",
            Retryable = false,
            ClientAction = ProblemDetailsClientAction.Wait_for_reconciliation,
            Details = new Details { Visibility = DetailsVisibility.Metadata_only },
        };
        TestSupport.CapturingHandler handler = new(HttpStatusCode.ServiceUnavailable, JsonConvert.SerializeObject(problem));
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await WorkspaceTools.PrepareWorkspace(
            pipeline,
            folderId: "folder_000000001",
            workspaceId: "workspace_000000001",
            idempotencyKey: "idempotency_000000001",
            taskId: "task_000000001",
            correlationId: "correlation-provider-unknown",
            requestJson: """{"requestSchemaVersion":"v2","repositoryBindingId":"binding_000000001","branchRefPolicyRef":"branchref_000000001","workspacePolicyRef":"workspacepolicy_000000001"}""",
            cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("unknown_provider_outcome");
    }

    [Fact]
    public async Task LockConflictProjectsExactWorkspaceLockedProblemThroughGeneratedSdk()
    {
        ProblemDetails problem = new()
        {
            Type = "about:blank",
            Title = "Workspace locked",
            Status = 409,
            Category = CanonicalErrorCategory.Lock_conflict,
            Code = CanonicalErrorCode.Workspace_locked,
            Message = "The workspace is locked by another task.",
            CorrelationId = "server-lock-correlation",
            Retryable = true,
            ClientAction = ProblemDetailsClientAction.Retry,
            Details = new Details { Visibility = DetailsVisibility.Metadata_only, LockStatus = "locked" },
        };
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Conflict, JsonConvert.SerializeObject(problem));
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await WorkspaceTools.LockWorkspace(
            pipeline,
            folderId: "folder_000000001",
            workspaceId: "workspace_000000001",
            idempotencyKey: "idempotency_000000001",
            taskId: "task_000000001",
            correlationId: "client-lock-correlation",
            requestJson: """{"requestSchemaVersion":"v2","lockIntent":"exclusive_write","requestedLeaseSeconds":60}""",
            cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject json = TestSupport.Parse(result);
        json.Value<string>("kind").ShouldBe("lock_conflict");
        json.Value<string>("code").ShouldBe("workspace_locked");
        json.Value<string>("correlationId").ShouldBe("server-lock-correlation");
        json.Value<bool>("retryable").ShouldBeTrue();
        json.Value<string>("clientAction").ShouldBe("retry");
        ((string?)json["details"]?["lockStatus"]).ShouldBe("locked");
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Uri?.AbsolutePath.ShouldBe("/api/v2/folders/folder_000000001/workspaces/workspace_000000001/lock");
    }

    [Theory]
    [InlineData("file_policy_unavailable")]
    [InlineData("range_unsatisfiable")]
    public async Task ExactFileProblemDtosPreserveCanonicalProjection(string category)
    {
        bool policyUnavailable = category == "file_policy_unavailable";
        string response = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = policyUnavailable ? "File policy unavailable" : "Range unsatisfiable",
            status = policyUnavailable ? 503 : 416,
            category,
            code = policyUnavailable ? "file_policy_unavailable" : "range_unsatisfiable",
            message = policyUnavailable
                ? "The file policy cannot be verified for this request."
                : "The requested byte range cannot be satisfied.",
            correlationId = "server-file-correlation",
            retryable = policyUnavailable,
            clientAction = policyUnavailable ? "retry" : "revise_request",
            details = new { visibility = policyUnavailable ? "redacted" : "metadata_only" },
        });
        TestSupport.CapturingHandler handler = new(
            policyUnavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.RequestedRangeNotSatisfiable,
            response);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await ContextTools.ReadFileRange(
            pipeline,
            folderId: "f",
            workspaceId: "w",
            taskId: "task-1",
            correlationId: "client-correlation",
            requestJson: "{\"requestSchemaVersion\":\"v2\",\"path\":{\"normalizedPath\":\"docs/readme.md\",\"displayName\":\"readme.md\",\"pathPolicyClass\":\"content_allowed\",\"unicodeNormalization\":\"NFC\"},\"startOffset\":0,\"endOffset\":1}",
            cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject json = TestSupport.Parse(result);
        json.Value<string>("kind").ShouldBe(category);
        json.Value<string>("correlationId").ShouldBe("server-file-correlation");
    }

    [Fact]
    public async Task ExactAuthorityUnavailableProjectsThroughGeneratedSdk()
    {
        string response = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = "Authorization evidence unavailable",
            status = 503,
            category = "read_model_unavailable",
            code = "projection_unavailable",
            message = "Authorization evidence is temporarily unavailable.",
            correlationId = "server-authority-correlation",
            retryable = true,
            clientAction = "retry",
            details = new { visibility = "redacted" },
        });
        TestSupport.CapturingHandler handler = new(HttpStatusCode.ServiceUnavailable, response);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline,
            folderId: "f",
            correlationId: "client-correlation",
            cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject json = TestSupport.Parse(result);
        json.Value<string>("kind").ShouldBe("read_model_unavailable");
        json.Value<string>("code").ShouldBe("projection_unavailable");
        json.Value<bool>("retryable").ShouldBeTrue();
        json.Value<string>("correlationId").ShouldBe("server-authority-correlation");
    }

    [Fact]
    public async Task NonCanonicalAuthorityTupleFailsClosedAsInternalError()
    {
        string response = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = "Authority unavailable",
            status = 503,
            category = "read_model_unavailable",
            code = "projection_unavailable",
            message = "Authorization evidence is temporarily unavailable.",
            correlationId = "server-authority-correlation",
            retryable = true,
            clientAction = "retry",
            details = new { visibility = "redacted" },
        });
        TestSupport.CapturingHandler handler = new(HttpStatusCode.ServiceUnavailable, response);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline,
            folderId: "f",
            correlationId: "client-correlation",
            cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Parse(result).Value<string>("kind").ShouldBe("internal_error");
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
