namespace Hexalith.Folders.Authorization;

public sealed class DenyAllEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(EventStoreAuthorizationValidationResult.Denied());
    }
}
