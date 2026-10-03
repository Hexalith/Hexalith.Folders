using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

internal sealed class HistoricalMutationAllowingValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator:1"));
}
