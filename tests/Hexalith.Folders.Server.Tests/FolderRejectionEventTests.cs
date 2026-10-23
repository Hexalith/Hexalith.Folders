using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderRejectionEventTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" LockConflict")]
    [InlineData("LockConflict ")]
    [InlineData("LockConflict\n")]
    [InlineData("lockconflict")]
    [InlineData("LOCKCONFLICT")]
    [InlineData("LockConflict, LockExpired")]
    public void NoncanonicalResultCodeShouldBeRejected(string code)
    {
        ArgumentException error = Should.Throw<ArgumentException>(() => Create(code));
        error.ParamName.ShouldBe("code");
        error.Message.Contains(code, StringComparison.Ordinal).ShouldBeFalse(
            "The exception must not echo the noncanonical code.");
    }

    [Fact]
    public void EveryDeclaredCanonicalResultCodeNameShouldBePreserved()
    {
        foreach (string name in Enum.GetNames<FolderResultCode>())
        {
            Create(name).Code.ShouldBe(name);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpecializedWorkspaceRejectionShouldPreserveCanonicalEvidence(bool duplicateLock)
    {
        IRejectionEvent rejection = CreateWorkspaceRejection(duplicateLock,
            FoldersServerModule.LockWorkspaceCommandType, "correlation-a", "task-a", "key-a");
        JsonElement payload = JsonSerializer.SerializeToElement(rejection, rejection.GetType());

        payload.GetProperty("Code").GetString().ShouldBe(duplicateLock
            ? nameof(FolderResultCode.LockConflict) : nameof(FolderResultCode.StateTransitionInvalid));
        payload.GetProperty("CommandType").GetString().ShouldBe(FoldersServerModule.LockWorkspaceCommandType);
        payload.GetProperty("CorrelationId").GetString().ShouldBe("correlation-a");
        payload.GetProperty("TaskId").GetString().ShouldBe("task-a");
        payload.GetProperty("IdempotencyKey").GetString().ShouldBe("key-a");
    }

    [Theory]
    [InlineData(false, "safe-identifier\n")]
    [InlineData(true, "safe-identifier\n")]
    [InlineData(false, "safe-identifier\r\n")]
    [InlineData(true, "safe-identifier\r\n")]
    [InlineData(false, "UPPERCASE")]
    [InlineData(true, "UPPERCASE")]
    [InlineData(false, "oversized")]
    [InlineData(true, "oversized")]
    public void SpecializedWorkspaceRejectionShouldDropNoncanonicalEvidence(bool duplicateLock, string shape)
    {
        string value = shape == "oversized" ? new string('a', 129) : shape;
        IRejectionEvent rejection = CreateWorkspaceRejection(duplicateLock, "Unsafe.Command\n", value, value, value);
        JsonElement payload = JsonSerializer.SerializeToElement(rejection, rejection.GetType());

        payload.GetProperty("CommandType").GetString().ShouldBe(FolderCommandRejected.UnknownCommandTypeSentinel);
        foreach (string name in new[] { "CorrelationId", "TaskId", "IdempotencyKey" })
        {
            (payload.GetProperty(name).ValueKind == JsonValueKind.Null).ShouldBeTrue(
                "Noncanonical workspace rejection identifiers must be dropped.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpecializedWorkspaceRejectionShouldRejectNullCommandType(bool duplicateLock)
        => Should.Throw<ArgumentNullException>(() => CreateWorkspaceRejection(duplicateLock, null!, null, null, null))
            .ParamName.ShouldBe("commandType");

    private static IRejectionEvent CreateWorkspaceRejection(bool duplicateLock, string commandType,
        string? correlationId, string? taskId, string? key)
        => duplicateLock
            ? DuplicateWorkspaceLockRejected.Create(commandType, correlationId, taskId, key)
            : WorkspaceTransitionInvalidRejected.Create(commandType, correlationId, taskId, key);

    private static FolderCommandRejected Create(string code)
        => FolderCommandRejected.Create(code, FoldersServerModule.ArchiveFolderCommandType,
            "tenant-a", "organization-a", "folder-a", "actor-a", "correlation-a", "task-a", "key-a");
}
