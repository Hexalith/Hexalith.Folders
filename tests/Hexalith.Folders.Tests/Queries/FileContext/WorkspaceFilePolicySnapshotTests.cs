using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.FileContext;

public sealed class WorkspaceFilePolicySnapshotTests
{
    [Theory]
    [InlineData("src/a.cs", "content_allowed")]
    [InlineData("SRC/a.cs", "content_allowed")]
    [InlineData("src/private/a.cs", "excluded")]
    [InlineData("src/restricted/a.cs", "restricted")]
    [InlineData("src/metadata/a.cs", "metadata_only")]
    [InlineData("outside/a.cs", "excluded")]
    [InlineData(".GiT/config", "restricted")]
    public void ServerRulesOwnEligibilityAndExclusionPrecedence(string value, string expected)
    {
        var policy = new WorkspaceFilePolicySnapshot("1.1.0", ["src/**", "src/private/**"], ["src/private/**"],
            ["src/restricted/**"], "non_confidential", ["src/metadata/**"]);
        policy.Classify(new(value, "entry", "content_allowed", "NFC")).ShouldBe(expected);
    }
    [Theory]
    [InlineData("**/file?.txt", "file1.txt", true)]
    [InlineData("**/file?.txt", "a/b/file1.txt", true)]
    [InlineData("src/*.cs", "src/a/b.cs", false)]
    [InlineData("src/*.cs", "SRC/test.CS", true)]
    [InlineData("[ab]", "a", false)]
    [InlineData("src/**.cs", "src/a.cs", false)]
    public void AnchoredGlobImplementsOnlyApprovedSegmentGrammar(string rule, string path, bool expected)
        => WorkspaceFilePolicySnapshot.Matches(rule, path).ShouldBe(expected);
    [Fact]
    public void SectionCountsPreventRuleTokensFromCollidingWithSectionLabels()
    {
        var first = new WorkspaceFilePolicySnapshot("1.1.0", ["**", "exclude"], ["safe.txt"], [], "non_confidential");
        var second = new WorkspaceFilePolicySnapshot("1.1.0", ["**"], ["exclude", "safe.txt"], [], "non_confidential");
        first.Classify(new("exclude", "exclude", "content_allowed", "NFC")).ShouldBe("content_allowed");
        second.Classify(new("exclude", "exclude", "content_allowed", "NFC")).ShouldBe("excluded");
        first.Digest.ShouldNotBe(second.Digest);
    }

    [Fact]
    public void SnapshotCopiesRulesAndHasNoPermissiveMissingPolicy()
    {
        string[] includes = ["src/**"];
        var snapshot = new WorkspaceFilePolicySnapshot("1.1.0", includes, [], [], "non_confidential");
        includes[0] = "**";
        snapshot.Classify(new("outside/a.txt", "entry", "content_allowed", "NFC")).ShouldBe("excluded");
        Should.Throw<InvalidOperationException>(() => new WorkspaceFilePolicySnapshot("1.1.0", [], [], [], "non_confidential"));
        Should.Throw<InvalidOperationException>(() => new WorkspaceFilePolicySnapshot("1.1.0", ["**"], [], [], ""));
        Should.Throw<InvalidOperationException>(() => new WorkspaceFilePolicySnapshot("1.1.0", ["**"], Enumerable.Repeat("a", 100), [], "non_confidential"));
    }
    [Fact]
    public void AcceptedPathCannotExceedApprovedFiveHundredCharacters()
    {
        WorkspacePathPolicyValidator.Validate(new(new string('a', 500), "entry", "content_allowed", "NFC")).IsAccepted.ShouldBeTrue();
        WorkspacePathPolicyValidator.Validate(new(new string('a', 501), "entry", "content_allowed", "NFC")).IsAccepted.ShouldBeFalse();
    }
}
