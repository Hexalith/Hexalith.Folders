using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

public sealed class IntegrationSmokeTests
{
    [Fact]
    public void IntegrationProjectIsCompileSafePlaceholder() => true.ShouldBeTrue();
}
