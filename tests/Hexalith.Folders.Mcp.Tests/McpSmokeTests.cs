using Shouldly;
using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

public sealed class McpSmokeTests
{
    [Fact]
    public void McpTestProjectCompilesAsAdapterGuard() => typeof(Program).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.Mcp");
}
