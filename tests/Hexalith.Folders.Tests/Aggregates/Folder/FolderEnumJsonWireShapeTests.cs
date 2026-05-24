using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

// Pins the name-based JSON wire shape for FolderArchiveReasonCode and FolderResultCode.
// The integer ordinal of these enum members is internal implementation detail. Wire
// payloads (rejection events, parity fixtures, log records, problem details) must serialize
// the enum NAME so that future renumbering or insertion cannot silently break consumers
// that already round-trip values across deploys.
public sealed class FolderEnumJsonWireShapeTests
{
    [Theory]
    [InlineData(FolderArchiveReasonCode.None, "\"None\"")]
    [InlineData(FolderArchiveReasonCode.CallerRequested, "\"CallerRequested\"")]
    [InlineData(FolderArchiveReasonCode.PolicyRetention, "\"PolicyRetention\"")]
    [InlineData(FolderArchiveReasonCode.OperatorReview, "\"OperatorReview\"")]
    public void FolderArchiveReasonCodeShouldSerializeAsEnumName(FolderArchiveReasonCode value, string expected)
    {
        string actual = JsonSerializer.Serialize(value);
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"CallerRequested\"", FolderArchiveReasonCode.CallerRequested)]
    [InlineData("\"PolicyRetention\"", FolderArchiveReasonCode.PolicyRetention)]
    [InlineData("\"OperatorReview\"", FolderArchiveReasonCode.OperatorReview)]
    [InlineData("\"None\"", FolderArchiveReasonCode.None)]
    public void FolderArchiveReasonCodeShouldDeserializeByName(string json, FolderArchiveReasonCode expected)
    {
        FolderArchiveReasonCode actual = JsonSerializer.Deserialize<FolderArchiveReasonCode>(json);
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FolderArchiveReasonCodeRoundTripsThroughString()
    {
        // The wire-shape contract is: serialization is always name-based, so consumers see
        // a stable token even after enum renumbering. Round-trip via the string
        // representation must preserve every supported value.
        foreach (FolderArchiveReasonCode value in Enum.GetValues<FolderArchiveReasonCode>())
        {
            string json = JsonSerializer.Serialize(value);
            json.ShouldStartWith("\"");
            FolderArchiveReasonCode roundTripped = JsonSerializer.Deserialize<FolderArchiveReasonCode>(json);
            roundTripped.ShouldBe(value);
        }
    }

    [Theory]
    [InlineData(FolderResultCode.Accepted, "\"Accepted\"")]
    [InlineData(FolderResultCode.MalformedJsonPayload, "\"MalformedJsonPayload\"")]
    [InlineData(FolderResultCode.AlreadyArchived, "\"AlreadyArchived\"")]
    [InlineData(FolderResultCode.TenantAccessDenied, "\"TenantAccessDenied\"")]
    public void FolderResultCodeShouldSerializeAsEnumName(FolderResultCode value, string expected)
    {
        string actual = JsonSerializer.Serialize(value);
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FolderResultCodeRoundTripsThroughString()
    {
        foreach (FolderResultCode value in Enum.GetValues<FolderResultCode>())
        {
            string json = JsonSerializer.Serialize(value);
            json.ShouldStartWith("\"");
            FolderResultCode roundTripped = JsonSerializer.Deserialize<FolderResultCode>(json);
            roundTripped.ShouldBe(value);
        }
    }
}
