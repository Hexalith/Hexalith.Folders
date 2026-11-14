using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Checks scope-derived rejection classification across primary actor and legacy projection replay.</summary>
public sealed class FolderRejectionReplayTests
{
    [Theory]
    [InlineData("folder-a")]
    [InlineData("tenant-a")]
    [InlineData("provider")]
    public void UnknownRejectionPrefixAllowsSubsequentOrdinaryOwnerCommand(string owner)
    {
        string aggregate = owner == "provider" ? ProviderBindingActorId.Create("binding-a") : owner;
        var rejection = FolderCommandRejected.Create("UnsupportedCommandType", "future unknown command", null, null,
            null, null, "correlation-a", "task-a", "rejected");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(rejection);
        var envelope = Envelope(aggregate, bytes);
        DomainServiceCurrentState prefix = new(null, [envelope], 0, 1);
        FolderEventReplay replay = new(new([typeof(FolderCreated), typeof(OrganizationAclBaselineInitialized), typeof(ProviderBindingConfigured)], []));
        ProjectionRequest request = new("tenant-a", "folders", aggregate,
            [new(typeof(FolderCommandRejected).FullName!, bytes, "json", 1, envelope.Metadata.Timestamp, "correlation-a")]);
        var projected = new FolderDomainProjectionHandler().Project(request);
        if (owner == "folder-a")
        {
            projected.ProjectionType.ShouldBe("folder-state");
            FolderState state = replay.FoldFolder(prefix, "tenant-a", aggregate);
            FolderAggregate.Handle(state, new CreateFolder("tenant-a", "tenant-a", aggregate, "Folder A", null,
                null, [], "user-a", "correlation-a", "task-a", "create-key", null), DateTimeOffset.UtcNow).Code.ShouldBe(FolderResultCode.Created);
        }
        else
        {
            projected.ProjectionType.ShouldBe("organization-provider-binding");
            OrganizationState state = replay.FoldOrganization(prefix, "tenant-a", aggregate);
            if (owner == "provider")
                OrganizationAggregate.Handle(state, new ConfigureProviderBinding("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                    "user-a", "binding-a", "forgejo", "credential-a", OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
                    "correlation-a", "task-a", "configure-key", DateTimeOffset.UtcNow, null)).Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
            else
                OrganizationAggregate.Handle(state, new InitializeOrganizationAclBaseline("tenant-a", "tenant-a", [],
                    "correlation-a", "task-a", "baseline-key", null)).Code.ShouldBe(OrganizationAclResultCode.Accepted);
        }
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Created")]
    [InlineData("IdempotentReplay")]
    [InlineData("AlreadyApplied")]
    [InlineData("999")]
    public void SuccessAndUndefinedRejectionsCannotPrimeAnyOwner(string code)
    {
        FolderEventReplay replay = new(new([typeof(FolderCreated)], []));
        foreach (string aggregate in new[] { "folder-a", "tenant-a", ProviderBindingActorId.Create("binding-a") })
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Code = code, CommandType = "future_command" });
            DomainServiceCurrentState source = new(null, [Envelope(aggregate, bytes)], 0, 1);
            if (aggregate == "folder-a") Should.Throw<InvalidOperationException>(() => replay.FoldFolder(source, "tenant-a", aggregate));
            else Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(source, "tenant-a", aggregate));
            Should.Throw<InvalidOperationException>(() => new FolderDomainProjectionHandler().Project(new("tenant-a", "folders", aggregate,
                [new(typeof(FolderCommandRejected).FullName!, bytes, "json", 1, DateTimeOffset.UtcNow, "correlation-a")])));
        }
    }

    private static EventEnvelope Envelope(string aggregate, byte[] bytes)
        => new(new EventMetadata("rejection-message", aggregate, "folder", "tenant-a", "folders", 1, 1,
            DateTimeOffset.UtcNow, "correlation-a", "causation-a", "user-a", "v1", typeof(FolderCommandRejected).FullName!, 1, "json"), bytes, null);
}
