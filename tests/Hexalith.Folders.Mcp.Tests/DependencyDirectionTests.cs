using System;
using System.Linq;

using Hexalith.Folders.Mcp.Tooling;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the thin-adapter dependency direction (AC #1): the MCP assembly references
/// <c>Hexalith.Folders.Client</c> but never the server, aggregates/workers, EventStore, or Dapr. The MCP
/// server is an adapter over the SDK only (MCP → Client → Contracts); pulling in any of those would mean it
/// had grown a parallel behavior surface.
/// </summary>
public sealed class DependencyDirectionTests
{
    private static readonly string[] ForbiddenReferencePrefixes =
    [
        "Hexalith.Folders.Server",
        "Hexalith.Folders.Workers",
        "Hexalith.EventStore",
        "Dapr",
    ];

    [Fact]
    public void McpAssemblyDoesNotReferenceServerWorkersEventStoreOrDapr()
    {
        string[] referenced = typeof(ToolPipeline).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (string forbidden in ForbiddenReferencePrefixes)
        {
            referenced.ShouldNotContain(
                name => name.StartsWith(forbidden, StringComparison.Ordinal),
                $"The MCP adapter must not reference '{forbidden}*' (dependency direction is MCP → Client → Contracts only).");
        }
    }

    [Fact]
    public void McpAssemblyReferencesTheFoldersClient()
    {
        string[] referenced = typeof(ToolPipeline).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldContain("Hexalith.Folders.Client");
    }
}
