using Hexalith.Folders.Server;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ServerSmokeTests
{
    [Fact]
    public void ServerModuleIsScaffoldOnly() => FoldersServerModule.Description.ShouldContain("server scaffold");
}
