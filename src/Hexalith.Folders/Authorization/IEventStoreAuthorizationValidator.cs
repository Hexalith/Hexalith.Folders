namespace Hexalith.Folders.Authorization;

public interface IEventStoreAuthorizationValidator
{
    Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default);
}
