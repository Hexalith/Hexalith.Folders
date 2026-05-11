using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests;

public sealed class FoldersModuleSmokeTests
{
    [Fact]
    public void CoreModuleExposesContractModuleName() => FoldersModule.Name.ShouldBe("Hexalith.Folders");
}
