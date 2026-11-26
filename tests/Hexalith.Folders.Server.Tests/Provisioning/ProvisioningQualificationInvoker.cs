using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server.Provisioning;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Supplies fixture callback attribution to Folders while the SDK actor owns all stream writes.</summary>
internal sealed class ProvisioningQualificationInvoker(IHttpContextAccessor accessor) : IDomainServiceInvoker
{
    internal const string IncumbentCommand = "fixture.seed-incumbent";
    internal const string OrganizationCommand = "fixture.seed-organization";
    internal const string RevokeCommand = "fixture.revoke-folder-manage";
    internal static readonly FolderEventReplay Replay = new(new(
        [typeof(FolderCreated), typeof(FolderAccessGranted), typeof(FolderAccessRevoked)], []));
    internal IOrganizationAclEvent[] OrganizationEvents { get; set; } = [];
    internal Func<FolderProvisioningService>? Service { get; set; }
    internal ConcurrentDictionary<string, (int Status, string? Outcome)> Observations { get; } = new(StringComparer.Ordinal);

    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? state, CancellationToken cancellationToken = default)
    {
        if (command.CommandType == OrganizationCommand)
        {
            return DomainResult.Success([.. OrganizationEvents]);
        }

        if (command.CommandType == IncumbentCommand)
        {
            // The actual generic create decision supplies the fixture's incumbent event.
            FolderResult result = FolderAggregate.Handle(FolderState.Empty,
                new CreateFolder(command.TenantId, command.TenantId, command.AggregateId, "Shared name",
                    null, null, [], "original-actor", "fixture-generic", "fixture-generic", "fixture-generic", null),
                DateTimeOffset.UtcNow);
            return DomainResult.Success([.. result.Events]);
        }

        if (command.CommandType == RevokeCommand)
        {
            const string actor = ProvisioningQualificationFixture.Actor;
            FolderState current = Replay.FoldFolder(state, command.TenantId, command.AggregateId);
            FolderResult revoked = FolderAggregate.Handle(current, new RevokeFolderAccess(command.TenantId, command.TenantId,
                command.AggregateId, [new(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, actor, "manage_folder_access")],
                actor, "fixture-revoke", "fixture-revoke", "fixture-revoke", null), ProvisioningQualificationClock.BeforeRestart);
            return DomainResult.Success([.. revoked.Events]);
        }

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
                 new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceProcess)],
                "fixture-eventstore")),
        };
        try
        {
            FolderProvisioningService service = Service?.Invoke() ?? throw new InvalidOperationException("No Folders fixture callback.");
            IResult result = await service.ProcessAsync(new(command, state), cancellationToken).ConfigureAwait(true);
            var wire = (result as IValueHttpResult)?.Value as DomainServiceWireResult;
            string? outcome = wire?.ResultPayload is { } json
                ? JsonSerializer.Deserialize<JsonElement>(json).GetProperty("status").GetString()
                : null;
            Observations[command.MessageId] = (((IStatusCodeHttpResult)result).StatusCode ?? 200, outcome);
            if (wire is null || wire.Events.Count == 0)
            {
                return DomainResult.NoOp();
            }

            return DomainResult.Success([.. wire.Events.Select(item => ReadFolderEvent(item.EventTypeName, item.Payload))]);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    internal static IFolderEvent ReadFolderEvent(string eventTypeName, byte[] payload)
    {
        Type type = typeof(IFolderEvent).Assembly.GetType(eventTypeName, throwOnError: true)!;
        return (IFolderEvent)(JsonSerializer.Deserialize(payload, type)
            ?? throw new InvalidOperationException("The persisted fixture event had no payload."));
    }
}
