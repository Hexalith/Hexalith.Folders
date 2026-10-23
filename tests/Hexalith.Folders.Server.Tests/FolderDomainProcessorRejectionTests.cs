using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderDomainProcessorRejectionTests
{
    [Theory]
    [InlineData("duplicate", nameof(FolderResultCode.LockConflict), typeof(DuplicateWorkspaceLockRejected))]
    [InlineData("transition-prepare", nameof(FolderResultCode.StateTransitionInvalid), typeof(WorkspaceTransitionInvalidRejected))]
    [InlineData("transition-commit", nameof(FolderResultCode.StateTransitionInvalid), typeof(WorkspaceTransitionInvalidRejected))]
    [InlineData("transition-lock", nameof(FolderResultCode.StateTransitionInvalid), typeof(WorkspaceTransitionInvalidRejected))]
    [InlineData("transition-release", nameof(FolderResultCode.StateTransitionInvalid), typeof(WorkspaceTransitionInvalidRejected))]
    [InlineData("not-owned", nameof(FolderResultCode.LockNotOwned), typeof(FolderCommandRejected))]
    [InlineData("expired", nameof(FolderResultCode.LockExpired), typeof(FolderCommandRejected))]
    [InlineData("unsafe-actor", nameof(FolderResultCode.MalformedEvidence), typeof(FolderCommandRejected))]
    public async Task ProcessorShouldSelectRejectionTypeAndCarryOnlySanitizedResultEvidence(
        string scenario, string expectedCode, Type expectedType)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        WorkspaceContentProductionFixture fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        using IServiceScope scope = fixture.Scope();
        IServiceProvider services = scope.ServiceProvider;
        FolderState state = fixture.State;
        InMemoryFolderRepository repository = services.GetRequiredService<IFolderRepository>().ShouldBeOfType<InMemoryFolderRepository>();
        FolderStreamName stream = FolderStreamName.Create("tenant-a", "folder-a");
        repository.AppendIfFingerprintAbsent(stream, "seed-key", "seed-fingerprint", fixture.Gateway.Events)
            .ShouldBe(FolderAppendOutcome.Appended);
        FolderState before = repository.Load(stream);
        bool release = scenario is "not-owned" or "expired" or "transition-release";
        string action = scenario switch
        {
            "transition-prepare" => WorkspacePreparationService.ActionToken,
            "transition-commit" => WorkspaceCommitService.ActionToken,
            _ => release ? WorkspaceLockReleaseService.ActionToken : WorkspaceLockAcquisitionService.ActionToken,
        };
        string commandType = scenario switch
        {
            "transition-prepare" => FoldersServerModule.PrepareWorkspaceCommandType,
            "transition-commit" => FoldersServerModule.CommitWorkspaceCommandType,
            _ => release ? FoldersServerModule.ReleaseWorkspaceLockCommandType : FoldersServerModule.LockWorkspaceCommandType,
        };
        string principal = scenario == "unsafe-actor" ? "user-token" : "user-a";
        string workspace = scenario.StartsWith("transition", StringComparison.Ordinal) ? "workspace-b" : "workspace-a";
        string proof = FolderCommandValidator.DeriveWorkspaceLockOwnershipProof(
            "tenant-a", "folder-a", workspace, "task-a", state.WorkspaceLockId!);
        if (scenario == "not-owned") proof = "wrong-proof";
        if (scenario == "expired") fixture.Clock.Now = fixture.Clock.Now.AddHours(2);

        object payload = release
            ? new { requestSchemaVersion = "v1", workspaceId = workspace, lockId = state.WorkspaceLockId, lockOwnershipProof = proof, releaseReasonCode = "caller_completed" }
            : new { requestSchemaVersion = "v1", workspaceId = workspace, lockIntent = "exclusive_write", requestedLeaseSeconds = 3600 };
        if (scenario == "transition-prepare") payload = new
        {
            requestSchemaVersion = "v1",
            workspaceId = workspace,
            repositoryBindingId = "binding-a",
            branchRefPolicyRef = "policy-a",
            workspacePolicyRef = "workspace-policy-a",
        };
        if (scenario == "transition-commit") payload = new
        {
            requestSchemaVersion = "v1",
            workspaceId = workspace,
            operationId = "commit-operation-a",
            taskId = "task-a",
            auditMetadataKeys = Array.Empty<string>(),
            authorMetadataReference = "authorref_service",
            branchRefTarget = "branchref_primary",
            commitMessageClassification = "generated_summary",
            changedPathMetadataDigest = "digest_workspace_a",
        };
        CommandEnvelope command = new("rejection-key-a", "tenant-a", "folders", "folder-a",
            commandType,
            JsonSerializer.SerializeToUtf8Bytes(payload), "rejection-correlation-a", null, principal,
            new Dictionary<string, string> { ["taskId"] = "task-a" });

        PreauthorizedRequestContext.Begin(new("tenant-a", principal, "folder-a", null, "tenant-a", action, action, null));
        ILayeredFolderAuthorizationResultAccessor accessor = services.GetRequiredService<ILayeredFolderAuthorizationResultAccessor>();
        try
        {
            LayeredFolderAuthorizationResult authorization = await services.GetRequiredService<LayeredFolderAuthorizationService>()
                .AuthorizeAsync(new("tenant-a", principal, principal, action, LayeredFolderOperationPolicy.Mutation(),
                    EventStoreClaimTransformEvidence.Allowed("tenant-a", principal, [action]), "folder-a",
                    "rejection-correlation-a", "task-a", null, null), TestContext.Current.CancellationToken).ConfigureAwait(true);
            authorization.IsAllowed.ShouldBeTrue();
            accessor.BeginScope(authorization);
            FolderDomainProcessor processor = services.GetServices<IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
            var result = await processor.ProcessAsync(command, null).ConfigureAwait(true);

            result.IsRejection.ShouldBeTrue();
            IEventPayload rejection = result.Events.ShouldHaveSingleItem();
            JsonElement evidence = JsonSerializer.SerializeToElement(rejection, rejection.GetType());
            evidence.GetProperty("Code").GetString().ShouldBe(expectedCode);
            rejection.GetType().ShouldBe(expectedType);
            rejection.ShouldBeAssignableTo<IRejectionEvent>();
            evidence.GetProperty("CommandType").GetString().ShouldBe(command.CommandType);
            evidence.GetProperty("CorrelationId").GetString().ShouldBe(command.CorrelationId);
            evidence.GetProperty("TaskId").GetString().ShouldBe("task-a");
            evidence.GetProperty("IdempotencyKey").GetString().ShouldBe(command.MessageId);
            if (rejection is FolderCommandRejected general)
            {
                general.ManagedTenantId.ShouldBe("tenant-a");
                general.OrganizationId.ShouldBe("tenant-a");
                general.FolderId.ShouldBe("folder-a");
                if (scenario == "unsafe-actor")
                {
                    (general.ActorPrincipalId is null).ShouldBeTrue(
                        "An aggregate-sanitized actor must not be replaced with the raw envelope user.");
                }
                else general.ActorPrincipalId.ShouldBe("user-a");
            }

            repository.EventsAppended.ShouldBe(5);
            repository.Load(stream).ShouldBeSameAs(before);
        }
        finally
        {
            accessor.EndScope();
            PreauthorizedRequestContext.End();
        }
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("folder")]
    [InlineData("message")]
    [InlineData("correlation")]
    [InlineData("actor")]
    [InlineData("task")]
    public async Task ProcessorShouldRejectTrailingLfBeforeCommandDispatch(string field)
    {
        WorkspaceContentProductionFixture fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        using IServiceScope scope = fixture.Scope();
        FolderDomainProcessor processor = scope.ServiceProvider.GetServices<IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
        CommandEnvelope command = new("key-a", "tenant-a", "folders", "folder-a", "Unsupported.Command",
            JsonSerializer.SerializeToUtf8Bytes(new { }), "correlation-a", null, "user-a",
            new Dictionary<string, string> { ["taskId"] = "task-a" });
        command = field switch
        {
            "tenant" => command with { TenantId = "tenant-a\n" },
            "folder" => command with { AggregateId = "folder-a\n" },
            "message" => command with { MessageId = "key-a\n" },
            "correlation" => command with { CorrelationId = "correlation-a\n" },
            "actor" => command with { UserId = "user-a\n" },
            _ => command with { Extensions = new Dictionary<string, string> { ["taskId"] = "task-a\n" } },
        };

        var result = await processor.ProcessAsync(command, null).ConfigureAwait(true);

        result.IsRejection.ShouldBeTrue();
        FolderCommandRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCommandRejected>();
        rejection.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
        JsonElement evidence = JsonSerializer.SerializeToElement(rejection);
        evidence.EnumerateObject().Any(property => property.Value.ValueKind == JsonValueKind.String
            && property.Value.GetString()!.Contains('\n', StringComparison.Ordinal)).ShouldBeFalse(
                "Rejected envelope evidence must not retain a newline.");
    }

    [Fact]
    public async Task UnsupportedCommandShouldUseEnvelopeFallbackWithoutEchoingItsActor()
    {
        WorkspaceContentProductionFixture fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        using IServiceScope scope = fixture.Scope();
        FolderDomainProcessor processor = scope.ServiceProvider.GetServices<IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
        CommandEnvelope command = new("fallback-key-a", "tenant-a", "folders", "folder-a", "Unsupported.Command",
            JsonSerializer.SerializeToUtf8Bytes(new { }), "fallback-correlation-a", null, "user-a",
            new Dictionary<string, string> { ["taskId"] = "fallback-task-a" });

        var result = await processor.ProcessAsync(command, null).ConfigureAwait(true);

        result.IsRejection.ShouldBeTrue();
        FolderCommandRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCommandRejected>();
        rejection.Code.ShouldBe(nameof(FolderResultCode.UnsupportedCommandType));
        rejection.CommandType.ShouldBe(command.CommandType);
        rejection.CorrelationId.ShouldBe(command.CorrelationId);
        rejection.TaskId.ShouldBe("fallback-task-a");
        rejection.IdempotencyKey.ShouldBe(command.MessageId);
        rejection.ActorPrincipalId.ShouldBeNull();
        rejection.ManagedTenantId.ShouldBeNull();
        rejection.OrganizationId.ShouldBeNull();
        rejection.FolderId.ShouldBeNull();
    }
}
