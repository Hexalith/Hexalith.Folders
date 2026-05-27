using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Resources;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the two read-only MCP resources (<c>folder-tree</c>, <c>audit-trail</c>) honor the same Adapter
/// Parity Contract behavior as the tools: they wrap an <see cref="IClient"/> query through the shared
/// <see cref="ToolPipeline"/> (correlation always echoed, task-id fail-closed where the operation is
/// task-scoped, credential short-circuit), declare no idempotency field, and stay metadata-only. They
/// introduce no new query semantics — AC #3.
/// </summary>
public sealed class ResourceTests
{
    [Fact]
    public async Task AuditTrailResourceWrapsListAuditTrailAndEchoesAFreshCorrelation()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, """{ "items": [], "page": { "hasMore": false } }""");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await AuditTrailResource.Read(pipeline, folderId: "f", TestContext.Current.CancellationToken);

        string? correlation = TestSupport.CorrelationId(result);
        correlation.ShouldNotBeNullOrWhiteSpace();
        correlation!.Length.ShouldBe(26); // fresh ULID — resources synthesize correlation when omitted
        TestSupport.Kind(result).ShouldBeNull(); // success envelope, not a failure
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].CorrelationId.ShouldBe(correlation);
    }

    [Fact]
    public async Task FolderTreeResourceIsTaskScopedAndFailsClosedWithoutTaskId()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTreeResource.Read(pipeline, folderId: "f", workspaceId: "w", taskId: " ", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task FolderTreeResourceThreadsTaskIdToTheWireWhenSupplied()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, """{ "items": [], "page": { "hasMore": false } }""");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTreeResource.Read(pipeline, folderId: "f", workspaceId: "w", taskId: "task-tree-1", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBeNull();
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].TaskId.ShouldBe("task-tree-1");
    }

    [Fact]
    public async Task ResourceShortCircuitsWithCredentialMissingBeforeAnyCall()
    {
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client, token: null);

        string result = await AuditTrailResource.Read(pipeline, folderId: "f", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("credential_missing");
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void ResourcesNeverDeclareAnIdempotencyKeyField()
    {
        IEnumerable<MethodInfo> resourceMethods = typeof(ToolPipeline).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerResourceAttribute>() is not null);

        int seen = 0;
        foreach (MethodInfo method in resourceMethods)
        {
            method.GetParameters().Any(p => p.Name == "idempotencyKey")
                .ShouldBeFalse($"Resource '{method.Name}' is read-only and must not declare an idempotencyKey field.");
            seen++;
        }

        seen.ShouldBe(2); // folder-tree + audit-trail
    }
}
