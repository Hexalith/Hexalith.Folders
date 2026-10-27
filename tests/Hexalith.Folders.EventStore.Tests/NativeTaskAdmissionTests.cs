using System.Reflection;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Validation;
using Hexalith.Folders.Server;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.EventStore.Tests;
public sealed class NativeTaskAdmissionTests
{
    [Theory]
    [InlineData("acquire")]
    [InlineData("renew")]
    [InlineData("release")]
    [InlineData("complete")]
    public void NativeCommandsPassRealRequestValidationAndExactKeyedAdmission(string operation)
    {
        string message = (string)typeof(FoldersDomainServiceEndpoints).GetMethod("GenerateCanonicalMessageId", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var payload = JsonSerializer.SerializeToElement(new { workspaceId = "workspace-a", taskId = "task-a", expectedHead = 42,
            request = new { operation, requestedLeaseSeconds = 300, lockId = "native_lock_a", ownershipProof = "proof-a", lockFence = 1,
                operationId = "operation-a", acceptedKey = "sha256:" + new string('a', 64), fingerprint = "fingerprint-a" } });
        var request = new SubmitCommandRequest(message, "tenant-a", "folders", "folder-a", FoldersServerModule.NativeTaskCommandType,
            payload, "correlation-a", new Dictionary<string, string> { ["taskId"] = "task-a" }, message);
        var validator = new SubmitCommandRequestValidator();
        validator.Validate(request).IsValid.ShouldBeTrue();
        validator.Validate(request with { MessageId = "native_bad" }).IsValid.ShouldBeFalse();
        var services = new ServiceCollection(); services.AddFoldersIdempotencyIntentAdapters();
        using var provider = services.BuildServiceProvider();
        var registry = new IdempotencyIntentAdapterRegistry(provider.GetServices<IIdempotencyIntentAdapter>(), new CanonicalIdempotencyIntentEncoder());
        var command = new SubmitCommand(message, "tenant-a", "folders", "folder-a", FoldersServerModule.NativeTaskCommandType,
            JsonSerializer.SerializeToUtf8Bytes(payload), "correlation-a", "user-a", new Dictionary<string, string> { ["taskId"] = "task-a" }, IdempotencyKey: message);
        var descriptor = registry.Resolve(command);
        descriptor.OperationId.ShouldBe("change-native-task"); descriptor.AdapterId.ShouldBe("hexalith-folders");
        descriptor.RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Mutation);
        var changed = JsonSerializer.SerializeToUtf8Bytes(new { workspaceId = "workspace-a", taskId = "task-a", expectedHead = 42,
            request = new { operation, requestedLeaseSeconds = 300, lockId = "native_lock_a", ownershipProof = "proof-a", lockFence = 2 } });
        registry.Resolve(command with { Payload = changed }).CanonicalIntent.ShouldNotBe(descriptor.CanonicalIntent);
    }
}
