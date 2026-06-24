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
    public void DiscoversAllToolsAndTwoResources()
    {
        ServiceCollection services = [];
        services
            .AddMcpServer()
            .WithToolsFromAssembly(typeof(ToolPipeline).Assembly)
            .WithResourcesFromAssembly(typeof(ToolPipeline).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        // 47 baseline + Story 10.5's two context-search facade tools (search-folder-indexed-files,
        // get-folder-indexing-status) = 49, keeping the MCP tool surface 1:1 with the canonical op spine.
        provider.GetServices<McpServerTool>().Count().ShouldBe(49);
        provider.GetServices<McpServerResource>().Count().ShouldBe(2);
    }
}
