using Hexalith.Folders.Contracts;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests;

public sealed class ContractsSmokeTests
{
    [Fact]
    public void ContractMetadataIdentifiesFoldersModule() => FoldersContractMetadata.ModuleName.ShouldBe("Hexalith.Folders");
}
