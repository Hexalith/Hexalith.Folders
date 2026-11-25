using System.Reflection;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Routing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests;

/// <summary>Keeps binaries built against earlier domain packages loading the moved contract types.</summary>
public sealed class ContractTypeForwardingTests
{
    [Fact]
    public void DomainAssemblyForwardsEveryMovedContractType()
    {
        Assembly contracts = typeof(FoldersCommandCatalog).Assembly;
        contracts.GetName().Name.ShouldBe("Hexalith.Folders.Contracts");
        string[] moved = contracts.GetExportedTypes()
            .Where(type => !type.IsNested
                && (type.Namespace == typeof(OrganizationStreamName).Namespace || type == typeof(FoldersCommandCatalog)))
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        moved.Length.ShouldBe(17);
        typeof(FolderStreamName).Assembly.GetForwardedTypes()
            .Where(type => !type.IsNested && type.Assembly == contracts)
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ShouldBe(moved);
    }
}
