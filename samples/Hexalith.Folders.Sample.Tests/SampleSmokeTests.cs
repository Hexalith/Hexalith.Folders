using Shouldly;
using Xunit;

namespace Hexalith.Folders.Sample.Tests;

public sealed class SampleSmokeTests
{
    [Fact]
    public void SampleProjectCompilesWithoutExternalServices() => typeof(Program).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.Sample");
}
