using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Fails if a composition-only diagnostic control attempts to issue authority.</summary>
internal sealed class DiagnosticCompositionAssertionIssuer : IWorkloadAssertionIssuer
{
    /// <inheritdoc/>
    public bool CanBindResources => true;

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Composition must not issue or dispatch a workload assertion.");
}
