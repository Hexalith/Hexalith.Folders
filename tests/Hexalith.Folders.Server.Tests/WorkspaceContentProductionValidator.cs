using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Records authorization contexts crossing the real production content-authority boundary.</summary>
internal sealed class WorkspaceContentProductionValidator : IEventStoreAuthorizationValidator
{
    /// <summary>Gets the validator contexts observed by the production content authority.</summary>
    internal List<EventStoreAuthorizationValidationRequest> Requests { get; } = [];

    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(EventStoreAuthorizationValidationResult.Allowed(null));
    }
}
