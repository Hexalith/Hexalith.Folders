using System.Reflection;
using System.Text.RegularExpressions;
using Hexalith.McpCli.Abstractions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

public sealed class FoldersWireNameMappingTests
{
    [Fact]
    public void EveryCommandWireMatchesTheDomainCatalogAndImmutableInventory()
    {
        string catalog = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "src/Hexalith.Folders/Routing/FoldersCommandCatalog.cs"));
        catalog.ShouldContain("string discriminator = \"Hexalith.Folders.Commands.\" + command;");
        var catalogNames = Regex.Matches(catalog, "Command\\(\"(?<name>[^\"]+)\", ")
            .Select(match => match.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        string[][] inventory = McpCliContractTestSupport.InventoryRows();
        OperationDeclaration[] commands = McpCliContractTestSupport.Operations.Where(row => row.Id.StartsWith('C')).ToArray();
        commands.Length.ShouldBe(13);
        foreach (OperationDeclaration row in commands)
        {
            HexalithCommandAttribute declaration = row.Type.GetCustomAttribute<HexalithCommandAttribute>()!;
            declaration.WireType.ShouldBe("Hexalith.Folders.Commands." + row.Type.Name, row.Id);
            catalogNames.ShouldContain(row.Type.Name, row.Id);
            string[] inventoryRow = inventory.Single(item => item[1] == row.Id);
            inventoryRow[3].ShouldBe(row.Type.FullName, row.Id);
            inventoryRow[4].ShouldBe(declaration.WireType, row.Id);

            // The catalog's task flag is part of command intent; do not infer it from field spelling.
            Match catalogRow = Regex.Match(catalog, "Command\\(\"" + row.Type.Name + "\",[\\s\\S]*?(?=\\n        Command\\(|\\n    \\]\\))");
            catalogRow.Success.ShouldBeTrue(row.Id);
            catalogRow.Value.Contains("task: true", StringComparison.Ordinal).ShouldBe(row.TaskScoped, row.Id);
        }
    }
}
