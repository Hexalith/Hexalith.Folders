namespace Hexalith.Projects.Server.Tests.Queries;

using Hexalith.Projects.Authorization;

internal sealed class ProjectReadTestAuthorization : IProjectEventStoreAuthorizationValidator
{
    public string? UnavailableProject { get; set; }
    public string Watermark { get; set; } = "eventstore_validator_test_allow";
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(EventStoreAuthorizationValidationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(request.ProjectId is not null && request.ProjectId == UnavailableProject
            ? EventStoreAuthorizationValidationResult.Unavailable() : EventStoreAuthorizationValidationResult.Allowed(Watermark));
}
