using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

internal sealed class OperationalStatusDeniedValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EventStoreAuthorizationValidationResult.Denied());
}
