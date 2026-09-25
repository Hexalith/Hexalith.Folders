using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class FinalAclAllowingValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator-a"));
}
