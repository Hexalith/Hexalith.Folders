namespace Hexalith.Folders.Authorization;

public sealed class AllowingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("eventstore_validator_not_configured"));
    }
}
