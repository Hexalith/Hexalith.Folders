using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Records the exact owner assertion requests made by the real SDK source clients.</summary>
internal sealed class OwnerSourceCompositionAssertionIssuer : IWorkloadAssertionIssuer
{
    /// <summary>Gets the original assertion requests.</summary>
    internal List<WorkloadAssertionRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public bool CanBindResources => true;

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return ValueTask.FromResult<string?>("owner-assertion");
    }
}
