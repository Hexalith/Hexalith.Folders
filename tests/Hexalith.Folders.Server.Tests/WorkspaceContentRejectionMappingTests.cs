using Hexalith.Folders.Aggregates.Folder;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Server.Content;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class WorkspaceContentRejectionMappingTests
{
    [Fact]
    public void RetainedDecisionMustBindTheUniqueCorrelationTaskAndProtectedExecution()
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Code = "AppendConflict",
            TaskId = "task",
            CorrelationId = "correlation",
            CommandType = FoldersServerModule.MutateFilesCommandType,
            IdempotencyKey = "protected-execution"
        });
        var source = new StreamReadEvent(1, typeof(FolderCommandRejected).FullName!, payload, "json", 1,
            "event-id", "correlation", "protected-execution", DateTimeOffset.UtcNow, "actor");
        WorkspaceContentRejectionReader.ReadCode([source], "correlation", "task").ShouldBe(FolderResultCode.AppendConflict);
        WorkspaceContentRejectionReader.ReadCode([source], "other-correlation", "task").ShouldBeNull();
        WorkspaceContentRejectionReader.ReadCode([source], "correlation", "other-task").ShouldBeNull();
        WorkspaceContentRejectionReader.ReadCode([source with { CausationId = "different-execution" }], "correlation", "task").ShouldBeNull();
        WorkspaceContentRejectionReader.ReadCode([source, source with { SequenceNumber = 2 }], "correlation", "task").ShouldBeNull();
    }
    [Theory]
    [InlineData(FolderResultCode.AppendConflict, 409)]
    [InlineData(FolderResultCode.ReplayConflict, 409)]
    [InlineData(FolderResultCode.ValidationFailed, 400)]
    [InlineData(FolderResultCode.MalformedEvidence, 400)]
    [InlineData(FolderResultCode.FolderAclDenied, 404)]
    [InlineData(FolderResultCode.PolicyEvidenceUnavailable, 503)]
    [InlineData(FolderResultCode.LockExpired, 410)]
    public void OnlyProvenConflictCodesBecomeConflicts(FolderResultCode code, int expectedStatus)
        => ((IStatusCodeHttpResult)FoldersDomainServiceEndpoints.ContentDomainProblem(code, "correlation", "task")).StatusCode.ShouldBe(expectedStatus);
}
