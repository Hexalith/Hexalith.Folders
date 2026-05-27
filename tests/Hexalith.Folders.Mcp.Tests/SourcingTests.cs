using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using ModelContextProtocol.Server;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the Adapter Parity Contract sourcing dimensions: correlation default generation + override echo
/// (both in the result and on the wire <c>X-Correlation-Id</c> header), task-ID propagation, and the
/// idempotency-key field schema rule (mutating tools declare it; query tools never do).
/// </summary>
public sealed class SourcingTests
{
    private const string AcceptedBody = "{}";

    private static readonly HashSet<string> MutatingToolNames = new(System.StringComparer.Ordinal)
    {
        "configure-provider-binding", "create-folder", "create-repository-backed-folder", "bind-repository",
        "archive-folder", "update-folder-acl-entry", "configure-branch-ref-policy", "prepare-workspace",
        "lock-workspace", "release-workspace-lock", "add-file", "change-file", "remove-file", "commit-workspace",
    };

    [Fact]
    public async Task DefaultCorrelationIsGeneratedEchoedAndSentOnTheWire()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedBody);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: null, requestJson: "{}", TestContext.Current.CancellationToken);

        string? correlation = TestSupport.CorrelationId(result);
        correlation.ShouldNotBeNullOrWhiteSpace();
        correlation!.Length.ShouldBe(26); // ULID shape from CorrelationAndTaskId.NewCorrelationId.
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].CorrelationId.ShouldBe(correlation);
    }

    [Fact]
    public async Task ExplicitCorrelationIsEchoedUnchangedAndSentOnTheWire()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, AcceptedBody);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-7", correlationId: "explicit-correlation-01", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.CorrelationId(result).ShouldBe("explicit-correlation-01");
        handler.Requests[0].CorrelationId.ShouldBe("explicit-correlation-01");
        handler.Requests[0].TaskId.ShouldBe("task-7");
    }

    [Fact]
    public void MutatingToolsDeclareIdempotencyKeyAndQueryToolsDoNot()
    {
        IEnumerable<MethodInfo> toolMethods = typeof(ToolPipeline).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

        int seen = 0;
        foreach (MethodInfo method in toolMethods)
        {
            string name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;
            bool declaresIdempotencyKey = method.GetParameters().Any(p => p.Name == "idempotencyKey");
            declaresIdempotencyKey.ShouldBe(
                MutatingToolNames.Contains(name),
                $"Tool '{name}' idempotencyKey field presence must match its mutating status.");
            seen++;
        }

        seen.ShouldBe(47); // All 47 canonical operations are exposed as tools.
    }
}
