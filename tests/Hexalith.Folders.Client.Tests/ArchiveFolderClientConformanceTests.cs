using System.Reflection;
using Hexalith.Folders.Client.Generated;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class ArchiveFolderClientConformanceTests
{
    private const string OperationName = "ArchiveFolderAsync";

    /// <summary>
    /// Pins the archive operation's return type, required parameter-type multiset, and parameter
    /// requiredness. Generated parameter names and positions are deliberately not asserted; header
    /// identity remains covered by the oracle-driven <c>TransportParityConformanceTests</c>.
    /// </summary>
    [Fact]
    public void GeneratedClientExposesArchiveFolderOperationWithRequiredSignature()
    {
        MethodInfo[] candidates = [.. typeof(IClient).GetMethods()
            .Where(static method => string.Equals(method.Name, OperationName, StringComparison.Ordinal))];

        candidates.ShouldNotBeEmpty($"{OperationName} must remain exposed on the generated client surface.");

        candidates
            .Any(static method => GeneratedClientMethodConformance.HasRequiredSignature(
                method,
                typeof(Task<AcceptedCommand>),
                GeneratedClientMethodConformance.ArchiveFolderParameterTypes))
            .ShouldBeTrue(
                $"{OperationName} must preserve its return type and required parameter-type multiset. Observed: "
                + GeneratedClientMethodConformance.DescribeOverloads(typeof(IClient), OperationName));
    }

    [Theory]
    [InlineData(ArchiveFolderRequestArchiveReasonCode.Caller_requested, "caller_requested")]
    [InlineData(ArchiveFolderRequestArchiveReasonCode.Policy_retention, "policy_retention")]
    [InlineData(ArchiveFolderRequestArchiveReasonCode.Operator_review, "operator_review")]
    public void GeneratedArchiveRequestSerializesSupportedReasonCodes(
        ArchiveFolderRequestArchiveReasonCode reasonCode,
        string expectedWireValue)
    {
        ArchiveFolderRequest request = new()
        {
            RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
            ArchiveReasonCode = reasonCode,
        };

        string json = JsonConvert.SerializeObject(request);

        json.ShouldContain("\"requestSchemaVersion\":\"v1\"");
        json.ShouldContain($"\"archiveReasonCode\":\"{expectedWireValue}\"");
    }
}
