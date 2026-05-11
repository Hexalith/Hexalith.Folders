using Shouldly;
using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class UiSmokeTests
{
    [Fact]
    public void UiTestProjectCompilesAsReadOnlyShellGuard() => typeof(Program).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.UI");
}
