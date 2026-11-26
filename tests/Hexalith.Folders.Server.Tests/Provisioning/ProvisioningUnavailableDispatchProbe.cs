using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Records the exact provisioning dispatch and simulates G-1 unavailability.</summary>
internal sealed class ProvisioningUnavailableDispatchProbe : IWorkloadAssertionIssuer
{
    public bool CanBindResources => true;

    public WorkloadAssertionRequest? Assertion { get; private set; }

    public string? Path { get; private set; }

    public ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assertion = request;
        return ValueTask.FromResult<string?>("isolated-test-assertion");
    }

    public IResult Respond(HttpContext context)
    {
        Path = context.Request.Path.Value;
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
