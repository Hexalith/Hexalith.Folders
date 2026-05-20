using System.Reflection;
using Hexalith.Folders.Client.Generated;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class ArchiveFolderClientConformanceTests
{
    [Fact]
    public void GeneratedClientExposesArchiveFolderOperationWithRequiredHeaders()
    {
        MethodInfo method = typeof(IClient).GetMethods()
            .Single(method => method.Name == "ArchiveFolderAsync" && method.GetParameters().Length == 6);

        method.ReturnType.ShouldBe(typeof(Task<AcceptedCommand>));
        method.GetParameters().Select(static parameter => parameter.Name).ToArray()
            .ShouldBe(["folderId", "idempotency_Key", "x_Correlation_Id", "x_Hexalith_Task_Id", "body", "cancellationToken"]);
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
