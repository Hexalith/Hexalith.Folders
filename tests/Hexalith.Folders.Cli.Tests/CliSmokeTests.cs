using Shouldly;
using Xunit;

namespace Hexalith.Folders.Cli.Tests;

public sealed class CliSmokeTests
{
    [Fact]
    public void CliTestProjectCompilesAsAdapterGuard() => typeof(Program).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.Cli");
}
