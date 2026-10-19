using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Server;

/// <summary>Rebuilds a Folders projection solely from the complete EventStore event prefix.</summary>
public sealed class FolderDomainProjectionHandler : IDomainProjectionHandler
{
    private static readonly PropertyInfo[] FolderStateProperties = typeof(FolderState).GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(static property => property.Name != nameof(FolderState.AccessOverrides)
            && property.Name != nameof(FolderState.TaskClosures)
            && property.Name != nameof(FolderState.WorkspaceCommitOperationId))
        .ToArray();

    /// <inheritdoc/>
    public string Domain => FoldersServerModule.DomainName;

    /// <inheritdoc/>
    public ProjectionResponse Project(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Domain, Domain, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId)
            || request.Events is null)
        {
            throw new InvalidOperationException("Invalid Folders projection request.");
        }

        ProjectionEventDto[] events = [.. request.Events];
        bool organization = events.Length == 0
            ? string.Equals(request.AggregateId, request.TenantId, StringComparison.Ordinal)
                || ProviderBindingActorId.IsReserved(request.AggregateId)
            : FolderEventReplay.IsOrganizationProjectionEvent(events[0]);
        if (events.Any(item =>
                FolderEventReplay.IsOrganizationProjectionEvent(item) != organization)
            || !organization && (string.Equals(request.AggregateId, request.TenantId, StringComparison.Ordinal)
                || ProviderBindingActorId.IsReserved(request.AggregateId))
            || organization && !string.Equals(request.AggregateId, request.TenantId, StringComparison.Ordinal)
                && !ProviderBindingActorId.IsReserved(request.AggregateId))
        {
            throw new InvalidOperationException("Mixed or reserved Folders projection stream.");
        }

        if (organization)
        {
            return new ProjectionResponse("organization-provider-binding",
                JsonSerializer.SerializeToElement(FolderEventReplay.FoldOrganization(request)));
        }

        FolderState state = FolderEventReplay.FoldFolder(request);
        Dictionary<string, object?> projection = new(StringComparer.Ordinal);
        foreach (PropertyInfo property in FolderStateProperties)
        {
            projection[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property.GetValue(state);
        }

        projection["accessOverrides"] = state.AccessOverrides.Values
            .OrderBy(static value => value.Key.CanonicalValue, StringComparer.Ordinal)
            .ToArray();
        return new ProjectionResponse("folder-state", JsonSerializer.SerializeToElement(projection));
    }
}
