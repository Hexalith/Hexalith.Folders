using Hexalith.Folders.Providers.GitHub;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class GitHubCanonicalEvidenceTests
{
    [Fact]
    public void CanonicalStringEncodingPinsNullEmptyUnicodeAndOrderVectors()
    {
        const string domain = "hxf-github:v1:test-vector";

        GitHubProviderSafeOperationEvidence.Create(domain, [null])
            .ShouldBe("407115f764d0328a4dc2fb131e963b500f01c90e2a5adccbc2f9cdce6773e09e");
        GitHubProviderSafeOperationEvidence.Create(domain, [string.Empty])
            .ShouldBe("64e3b0dda4905f2185f24c9dd89b26fb2870c44a089d4da6862aac8931cb5ca9");
        GitHubProviderSafeOperationEvidence.Create(domain, ["e\u0301"])
            .ShouldBe("39c207d41556d1299df7003b2cb680fe296aa090bb277c7c0516ce54627d000f");
        GitHubProviderSafeOperationEvidence.Create(domain, ["A", "B"])
            .ShouldBe("39967ca745d30fd7a095dfb941e5cdbfb3315ec15b8c7c93422b8666ca7b65d9");
        GitHubProviderSafeOperationEvidence.Create(domain, ["A", "B"])
            .ShouldNotBe(GitHubProviderSafeOperationEvidence.Create(domain, ["B", "A"]));
    }

    [Theory]
    [InlineData("mutation-target", "bcbfef6dfa297f449316b6c7c342efe097721cf33d04e4fdf2d246728d57ddc6")]
    [InlineData("commit-target", "a12171cc8ed5b44c4445efed4bdb0f16abd47341dc90cc9d570c4268c8f20553")]
    [InlineData("status-target", "0e6014e873b18c42dd588a8630ae170acc50592997f1ca6deddea1c36a3c6d8a")]
    [InlineData("path", "c5c25fbb6bc8a73edef09f59219e549285bf54cb4820d1c9a932a6f2121c6ac3")]
    [InlineData("content", "ba49593ba245a3d169e4a824d46d2ffdd613a4892e263d672f4e7e8056329a5c")]
    [InlineData("change-set", "18715e19c50bebb619e653531ba655046f9f2650111d6400def8f3b5572eb390")]
    [InlineData("staged-tree", "623b71888ff91fc764de7178b84193e7c4302331913d7250d5569e571d5d9514")]
    [InlineData("commit-message", "bba3e95b85d829572de3bec3b0fa42c1dd5d406e3aaca786ea03684fdd163a54")]
    [InlineData("expected-head", "6894a3291c930b99c0bd174e5a23f29f377fb82f84f5385a8ccb8c6af7677f4b")]
    [InlineData("full-ref", "75e2f09d15f74da771650d74c15294b381c36ebbd2af842841f057a8edfc9036")]
    [InlineData("status-expected-head", "577544458d77960a72a3d32a565c4ec9b5a3b7ce2ab6081e1ef396d3861121bd")]
    [InlineData("intended-commit", "68538781c3bcb41267e62c73313a1ba37df51011178abad2023ae333808f2b3d")]
    [InlineData("status-window", "9cb2bf930826788ed65d2900c94ca9c98837aa13ceda12d8bd06579f410b8840")]
    [InlineData("operation-target", "2f53cd9b71a049ab7569da90d152d2de47a90864568b022c377a4df586c7df75")]
    [InlineData("mutation-outcome", "3c28cc13a9a5e4c3ead4880cf9c99947e8464ad89bcb78f5db22fb6ea3a54f42")]
    [InlineData("commit-outcome", "c378faf33d9d9cf045d8b9ad2020248b8ace31280f97fc8dfe6bfa6fc97b69aa")]
    [InlineData("status-observation", "ab2705e5bd3dea35a3df68815441b5ad93ca8d72c63521fe3e3c650a1682dd73")]
    [InlineData("mutation-failure", "96d129b2833def82814ef57489c7ec1673cd94dd307e1501c1a57bdfd2e3122d")]
    [InlineData("commit-failure", "19fb1e85e314a5d49e30029c01e4f7a1e6fc1f31693e3f50ac925577e014568c")]
    public void SourceAndOutcomeDomainsRemainVersionedAndSeparated(string purpose, string expected)
        => GitHubProviderSafeOperationEvidence.Create($"hxf-github:v1:{purpose}", ["A"]).ShouldBe(expected);
}
