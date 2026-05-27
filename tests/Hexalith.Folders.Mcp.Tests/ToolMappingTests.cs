using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the deterministic 1:1 tool→operation mapping the Story 5.4/5.6 parity runs depend on (AC #2):
/// every MCP tool name is kebab-case, resolves to exactly one <see cref="IClient"/> operation
/// (PascalCase(name) + <c>Async</c>), tool names are unique, and the auto-transport <c>add-file</c>/
/// <c>change-file</c> distinction is preserved (the two operation IDs are never collapsed into one tool).
/// </summary>
public sealed class ToolMappingTests
{
    private static IReadOnlyList<(string Name, MethodInfo Method)> ToolMethods()
        => typeof(ToolPipeline).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(m => (m.GetCustomAttribute<McpServerToolAttribute>()!.Name!, m))
            .ToList();

    [Fact]
    public void EveryToolNameIsKebabCaseAndResolvesToExactlyOneIClientOperation()
    {
        HashSet<string> clientOperations = typeof(IClient)
            .GetMethods()
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string name, _) in ToolMethods())
        {
            name.ShouldMatch("^[a-z0-9]+(-[a-z0-9]+)*$", $"Tool name '{name}' must be kebab-case.");

            string expectedOperation = KebabToPascal(name) + "Async";
            clientOperations.ShouldContain(
                expectedOperation,
                $"Tool '{name}' must map 1:1 to IClient operation '{expectedOperation}'.");
        }
    }

    [Fact]
    public void ToolNamesAreUnique()
    {
        List<string> names = ToolMethods().Select(t => t.Name).ToList();
        names.Count.ShouldBe(names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AddFileAndChangeFileAreDistinctTools()
    {
        List<string> names = ToolMethods().Select(t => t.Name).ToList();

        names.ShouldContain("add-file");
        names.ShouldContain("change-file");
        // Never merged into a single "write-file": the per-operation_id oracle mapping must survive.
        names.ShouldNotContain("write-file");
    }

    private static string KebabToPascal(string kebab)
        => string.Concat(kebab
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpper(segment[0], CultureInfo.InvariantCulture) + segment[1..]));
}
