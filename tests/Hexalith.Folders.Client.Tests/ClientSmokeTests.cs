using Hexalith.Folders.Client;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class ClientSmokeTests
{
    [Fact]
    public void ClientStaysContractCentered() => FoldersClientModule.Name.ShouldBe("Hexalith.Folders");
}
