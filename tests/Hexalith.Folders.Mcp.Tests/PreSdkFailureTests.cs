using System.Linq;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the pre-SDK failure classes (<c>usage_error</c>, <c>credential_missing</c>) short-circuit before
/// any HTTP call: the fake <see cref="IClient"/> records no calls. These are mutually exclusive with the
/// post-SDK kinds and carry the canonical fields.
/// </summary>
public sealed class PreSdkFailureTests
{
    [Fact]
    public async Task MissingIdempotencyKeyOnMutatingToolIsUsageErrorWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "", taskId: "task-1", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        TestSupport.CorrelationId(result).ShouldBe("corr-1");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTaskIdOnTaskScopedToolIsUsageErrorWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await WorkspaceTools.PrepareWorkspace(
            pipeline, folderId: "f", workspaceId: "w", idempotencyKey: "idem-1", taskId: " ", correlationId: "corr-2", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        TestSupport.CorrelationId(result).ShouldBe("corr-2");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTaskIdOnTaskScopedQueryIsUsageErrorWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await ContextTools.ListFolderFiles(
            pipeline, folderId: "f", workspaceId: "w", taskId: "", correlationId: "corr-3", cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingCredentialIsCredentialMissingWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client, token: null);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-4", requestJson: "{}", TestContext.Current.CancellationToken);

        JObjectAssert(result);
        client.ReceivedCalls().ShouldBeEmpty();

        static void JObjectAssert(string json)
        {
            Newtonsoft.Json.Linq.JObject o = TestSupport.Parse(json);
            o.Value<string>("kind").ShouldBe("credential_missing");
            o.Value<string>("correlationId").ShouldBe("corr-4");
            o.Value<string>("code").ShouldBe("credential_missing");
            o.Value<bool>("retryable").ShouldBeFalse();
            o.Value<string>("clientAction").ShouldBe("check_credentials");
        }
    }

    [Fact]
    public async Task MissingCredentialOnQueryIsCredentialMissingWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client, token: null);

        string result = await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "corr-5", cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("credential_missing");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MalformedRequestBodyIsUsageErrorWithNoCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-6", requestJson: "{ not json", cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        client.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IClient.CreateFolderAsync)).ShouldBeFalse();
    }

    [Fact]
    public async Task UsageErrorCarriesTheFullCanonicalFieldSet()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "", taskId: "task-1", correlationId: "corr-fields", requestJson: "{}", TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject o = TestSupport.Parse(result);
        o.Value<string>("kind").ShouldBe("usage_error");
        o.Value<string>("correlationId").ShouldBe("corr-fields");
        o.Value<string>("code").ShouldBe("client_configuration_error"); // canonical category for pre-SDK usage
        o.Value<bool>("retryable").ShouldBeFalse();
        o.Value<string>("clientAction").ShouldBe("revise_request");
    }
}
