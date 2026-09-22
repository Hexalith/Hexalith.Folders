using Hexalith.Folders.Cli.Commands;
using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Client.Generated;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

public sealed class CommandOptionsFreshnessTests
{
    [Fact]
    public void ParseFreshnessShouldTreatOmissionAsTheDefault()
        => CommandOptions.ParseFreshness(null).ShouldBeNull();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("read_your_writes_now")]
    public void ParseFreshnessShouldRejectSuppliedInvalidValues(string freshness)
        => Should.Throw<CliUsageException>(() => CommandOptions.ParseFreshness(freshness));

    [Fact]
    public void ParseFreshnessShouldMapTheClosedVocabulary()
        => CommandOptions.ParseFreshness("eventually_consistent").ShouldBe(ReadConsistencyClass.Eventually_consistent);
}
