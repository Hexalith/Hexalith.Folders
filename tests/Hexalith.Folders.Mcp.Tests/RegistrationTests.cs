using System.Linq;

using Hexalith.Folders.Mcp.Tooling;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the MCP SDK discovers the full tool and resource surface from the assembly attributes without
/// throwing (this validates the <c>[McpServerTool]</c>/<c>[McpServerResource]</c> wiring, including the
/// resource URI templates, at test time rather than only at server start).
/// </summary>
public sealed class RegistrationTests
{
    [Fact]
    public void DiscoversAllFortySevenToolsAndTwoResources()
    {
        ServiceCollection services = [];
        services
            .AddMcpServer()
            .WithToolsFromAssembly(typeof(ToolPipeline).Assembly)
            .WithResourcesFromAssembly(typeof(ToolPipeline).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<McpServerTool>().Count().ShouldBe(47);
        provider.GetServices<McpServerResource>().Count().ShouldBe(2);
    }
}
