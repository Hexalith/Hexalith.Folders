using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
{
    [Theory]
    [InlineData("!!str review_by")]
    [InlineData("! review_by")]
    [InlineData("'review_by'")]
    public void StringReviewDateKeysCannotBypassValidation(string key)
    {
        EvaluateSyntheticReviewDate("[]", key).ShouldHaveSingleItem().Category.ShouldBe("approval_date_invalid");
        EvaluateSyntheticReviewDate("2026-10-29", key).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("!!int review_by: 2026-10-29")]
    [InlineData("review_by: 2026-10-29\n!!str review_by: 2026-10-30")]
    public void ReviewDatesRejectInvalidKeyTagsAndSemanticDuplicates(string yaml)
    {
        YamlMappingNode row = SyntheticApprovalRow("C-REVIEW", ["PM"], [("PM", "Jerome", "2026-10-18")]);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in ParseGovernanceTestYaml(yaml).Children)
        {
            RequiredMapping(row, "approval").Add(entry.Key, entry.Value);
        }

        GateDiagnostic diagnostic = EvaluateApprovalRecords(row, SyntheticApprovalPolicy(), new DateOnly(2026, 10, 28)).ShouldHaveSingleItem();
        diagnostic.Category.ShouldBe("approval_date_invalid");
        AssertMetadataOnly(diagnostic.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("~")]
    [InlineData("''")]
    [InlineData("'  '")]
    [InlineData("{ provider_token_private_marker: value }")]
    [InlineData("[provider_token_private_marker]")]
    [InlineData("'provider_token_private_marker'")]
    [InlineData("'2026-13-40'")]
    [InlineData("'2026-02-29'")]
    [InlineData("true")]
    [InlineData("123")]
    [InlineData("!!timestamp '2026-10-29'")]
    [InlineData("!!int '2026-10-29'")]
    [InlineData("!!null '2026-10-29'")]
    [InlineData("!provider_token_private_marker '2026-10-29'")]
    public void PresentMalformedReviewDateProducesOneBoundedDiagnostic(string yaml)
    {
        GateDiagnostic diagnostic = EvaluateSyntheticReviewDate(yaml).ShouldHaveSingleItem();

        diagnostic.Gate.ShouldBe("exit-criteria");
        diagnostic.Category.ShouldBe("approval_date_invalid");
        diagnostic.Identifier.ShouldBe("C-REVIEW");
        diagnostic.ToString().ShouldBe("exit-criteria:approval_date_invalid: id=C-REVIEW; path=docs/exit-criteria/c0-c13-governance-evidence.yaml");
        diagnostic.ToString().ShouldNotContain("private_marker");
        AssertMetadataOnly(diagnostic.ToString());
    }

    [Theory]
    [InlineData("2026-10-29")]
    [InlineData("'2026-10-29'")]
    [InlineData("\"2026-10-29\"")]
    [InlineData("!!str 2026-10-29")]
    [InlineData("! 2026-10-29")]
    public void FutureStringReviewDatesRemainValid(string yaml) =>
        EvaluateSyntheticReviewDate(yaml).ShouldBeEmpty();

    [Theory]
    [InlineData("2026-10-28")]
    [InlineData("2026-10-27")]
    [InlineData("!!str 2026-10-28")]
    public void TodayAndPastReviewDatesProduceOneStaleDiagnostic(string yaml)
    {
        GateDiagnostic diagnostic = EvaluateSyntheticReviewDate(yaml).ShouldHaveSingleItem();

        diagnostic.Category.ShouldBe("approval_stale");
        diagnostic.Identifier.ShouldBe("C-REVIEW");
        AssertMetadataOnly(diagnostic.ToString());
    }

    [Fact]
    public void MissingReviewDateRemainsOptional() =>
        EvaluateApprovalRecords(
            SyntheticApprovalRow("C-REVIEW", ["PM"], [("PM", "Jerome", "2026-10-18")]),
            SyntheticApprovalPolicy(),
            new DateOnly(2026, 10, 28)).ShouldBeEmpty();

    private static GateDiagnostic[] EvaluateSyntheticReviewDate(string yaml, string key = "review_by")
    {
        YamlMappingNode row = SyntheticApprovalRow("C-REVIEW", ["PM"], [("PM", "Jerome", "2026-10-18")]);
        KeyValuePair<YamlNode, YamlNode> entry = ParseGovernanceTestYaml(key + ": " + yaml).Children.Single();
        RequiredMapping(row, "approval").Add(entry.Key, entry.Value);

        return EvaluateApprovalRecords(row, SyntheticApprovalPolicy(), new DateOnly(2026, 10, 28));
    }
}
